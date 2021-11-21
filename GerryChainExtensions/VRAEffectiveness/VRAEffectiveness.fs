namespace VRA

open FSharp.Data
open Deedle

module VRAEffectiveness =

    type MinorityGroup = string //Hispanic | Black | Asian | AMIN | Neither

    type Column = string
    type Year = string

    type CVAPColumn = Map<Year, Column>

    module Minority = 
        type Name = MinorityGroup
        type MinCVAPCol = CVAPColumn
        type TotCVAPCol = CVAPColumn
        type Minority = Name * MinCVAPCol * TotCVAPCol


    type District<'a when 'a : equality> = Frame<'a, Column>

    /// Defines an election with some set of candidates
    type Election(cands: Column array) = 
        member this.candidates = cands

        /// <summary>
        /// Get vote share for candidates in the election.
        /// </summary>
        /// <param name="districtData"> Frame where each row is a precinct within the district and contains
        /// candidate columns.
        /// </param>
        /// <typeparam name="'a">The type of the Precinct Key index.  Should be string or int</typeparam>
        /// <returns> Series of candidates and their vote share.  </returns>
        member this.VoteShares (districtData: District<'a>) = 
            let voteCounts = districtData |> Frame.sliceCols this.candidates
                                          |> Frame.reduceValues (+)
            let totalVotes = Stats.sum voteCounts
            match totalVotes with 
            | 0. -> voteCounts
            | _ -> voteCounts |> Series.mapValues (fun a -> float a / float totalVotes)

    type CandidatesOfChoice = Map<Minority.Name,Column>

    /// Defines an election group for a primary and potential run-offs/generals
    type ElectionGroup = 
        {
            Name: string
            Year: Year
            Score: Map<Minority.Name, float>
            Primary: Election
            PrimaryCoC: CandidatesOfChoice
            Runoff: Election option
            RunoffCoC: CandidatesOfChoice option
            General: Election option
            GeneralCoC: CandidatesOfChoice option
        }

    type LogitParams = 
        {
            Coef: float
            Intercept: float
        }

    /// Class representing Json Parser for state.
    /// Takes the JsonValue (from FSharp.dData) and defines instance members Minorities and Elections
    type Parser (data: JsonValue) =
        let ParseCVAPcol (data: JsonValue) = 
            let cvapData = data.["cvap_col"]
            match cvapData with 
            | JsonValue.Record(cvapYears) -> cvapYears |> Array.map (fun (k,v) -> k, v.AsString())
                                                       |> Map.ofArray
            | _ -> Map.empty

        let getLogitParams (details: JsonValue) = 
            let logit_params = details.["logit_params"]
            match logit_params with 
            | JsonValue.Record([|("coef", coef); ("intercept", intercept)|]) -> Some {Coef = coef.AsFloat(); Intercept=intercept.AsFloat()}
            | _ -> None

        let ParseRunoffGeneralCands = function
            | JsonValue.Array(cands) -> cands |> Array.map (fun v -> v.AsString()) |> Election |> Some
            | _ -> None

        let ParseRunoffGeneralCoC = function
            | JsonValue.Record(cocs) -> cocs |> Array.map (fun (k,v) -> k, v.AsString()) 
                                             |> Map.ofArray |> Some
            | _ -> None

        let getElectionGroup (electgroup: JsonValue) =
            let name = electgroup.["name"].AsString()
            let year = electgroup.["year"].AsString()
            let scores = electgroup.["score"].Properties() 
                       |> Array.map (fun (k,v) -> k, v.AsFloat()) |> Map.ofArray
        
            let primary = electgroup.["primary_cands"].AsArray() 
                             |> Array.map (fun v -> v.AsString()) |> Election
            let primaryCoC = electgroup.["primary_coc"].Properties()
                            |> Array.map (fun (k,v) -> k, v.AsString()) |> Map.ofArray
        
            let runoff = ParseRunoffGeneralCands electgroup.["runoff_cands"]
            let runoffCoC = ParseRunoffGeneralCoC electgroup.["runoff_coc"]

            let general = ParseRunoffGeneralCands electgroup.["general_cands"]
            let generalCoC = ParseRunoffGeneralCoC electgroup.["general_coc"]

            {Name=name; Year=year; Score=scores; Primary=primary; PrimaryCoC=primaryCoC; Runoff=runoff; 
             RunoffCoC=runoffCoC; General=general;GeneralCoC=generalCoC}


        member this.Minorities = data.["minorities"].Properties() 
                               |> Array.map (fun (name, details) -> (Minority.Minority(name, ParseCVAPcol details, ParseCVAPcol data),
                                                                     getLogitParams details))

        member this.Elections = match data.["election_groups"] with
                                | JsonValue.Array(elements) -> elements |> Array.map getElectionGroup
                                | _ -> [||]
    
        member this.AlignmentYear = data.["alignment_year"].AsString()

    type PlanVRAScores<'a when 'a : comparison > = Map<Minority.Name, Map<'a, float>>

    type ElectionDetails = 
        {
            Name: string
            CoC: Column
            CoCPerc: float
            CoCPlace: int
            FirstPlacePerc: float
            NumCands: int
            ExistsRunoff: bool
            CoCRO: Column
            CoCPercRO: float
            ExistsGen: bool
            CoCGen: Column
            CoCPercGen: float
            GroupControl: float
        }

    type DistrictSummary = 
        {
            Score: float
            CVAPShare: float
            ElectionDetails: ElectionDetails array
        }


    type PlanVRASummary<'a when 'a : comparison > = Map<Minority.Name, Map<'a, DistrictSummary>>


    type SuccessFunction<'a when 'a : equality> =  District<'a> -> Minority.Name -> ElectionGroup -> float

    type AlignmentFunction<'a when 'a : equality> = District<'a> -> Minority.Minority -> Year -> float

    /// <summary>
    ///     Represents Success of a candidate of choice with respect to the passed election group in
    ///     places that use a plurality system for the primary.
    /// </summary>
    /// <param name="district"></param> Frame where each row is a precinct within the district and contains
    /// candidate columns.
    /// <param name="minority"> The minority group to calculate CoC success for </param>
    /// <param name="election"> The election group to calculate success for </param>
    /// <typeparam name="'a">The type of the Precinct Key index.   Should be string or int</typeparam>
    /// <returns> 1. if the CoC was successful and 0. otherwise.</returns>
    let CoCCarriesElectPlurality (district: District<'a>) (minority: Minority.Name) (election: ElectionGroup) = 
        let PrimaryVoteShares = election.Primary.VoteShares district |> Series.sortBy (fun v -> - v)
        let PrimCoCVoteShare = PrimaryVoteShares.[election.PrimaryCoC.[minority]]
    
        let PrimaryWin = match PrimCoCVoteShare with
                         | x when (Series.getAt 0 PrimaryVoteShares = x) -> 1.
                         | _ -> 0.

        let GenWin = match election.General, election.GeneralCoC with
                     | None, None -> 1.
                     | Some election, Some cand when (election.VoteShares district).[cand.[minority]] > 0.5 -> 1.
                     | _ -> 0.
        PrimaryWin * GenWin


    /// <summary>
    ///     Represents Success of a candidate of choice in LA with respect to the passed election group
    /// </summary>
    /// <param name="district"></param> Frame where each row is a precinct within the district and contains
    /// candidate columns.
    /// <param name="minority"> The minority group to calculate CoC success for </param>
    /// <param name="election"> The election group to calculate success for </param>
    /// <typeparam name="'a">The type of the Precinct Key index.   Should be string or int</typeparam>
    /// <returns> 1. if the CoC was successful and 0. otherwise.</returns>
    let CoCCarriesElectLA (district: District<'a>) (minority: Minority.Name) (election: ElectionGroup) = 
            let PrimaryVoteShares = election.Primary.VoteShares district |> Series.sortBy (fun v -> - v)
            let PrimCoCVoteShare = PrimaryVoteShares.[election.PrimaryCoC.[minority]]
            let PrimaryAdvance = match PrimCoCVoteShare with
                                 | maj when maj > 0.5 -> 1.
                                 | x when Stats.max PrimaryVoteShares < 0.5 && (Series.getAt 0 PrimaryVoteShares = x) -> 1.
                                 | x when Stats.max PrimaryVoteShares < 0.5 && (Series.getAt 1 PrimaryVoteShares = x) -> 1.
                                 | _ -> 0.
            let GenWin = match election.General, election.GeneralCoC with
                         | None, None -> 1.
                         | Some election, Some cand when (election.VoteShares district).[cand.[minority]] > 0.5 -> 1.
                         | _ -> 0.
        
            match election.Name.[0..3] with
            | "PRES" when (Series.getAt 0 PrimaryVoteShares = PrimCoCVoteShare) -> GenWin
            | "PRES" -> 0.
            | _ -> PrimaryAdvance * GenWin


    /// <summary>
    ///     Represents Success of a candidate of choice in TX with respect to the passed election group
    /// </summary>
    /// <param name="district"></param> Frame where each row is a precinct within the district and contains
    /// candidate columns.
    /// <param name="minority"> The minority group to calculate CoC success for </param>
    /// <param name="election"> The election group to calculate success for </param>
    /// <typeparam name="'a">The type of the Precinct Key index.   Should be string or int</typeparam>
    /// <returns> 1. if the CoC was successful and 0. otherwise.</returns>
    let CoCCarriesElectTX (district: District<'a>) (minority: Minority.Name) (election: ElectionGroup) = 
            let PrimaryVoteShares = election.Primary.VoteShares district |> Series.sortBy (fun v -> - v)
            let PrimCoCVoteShare = PrimaryVoteShares.[election.PrimaryCoC.[minority]]
            // printf "prim coc vote share: %f" <| PrimCoCVoteShare
            let PrimaryAdvance = match PrimCoCVoteShare with
                                 | maj when maj > 0.5 -> 1.
                                 | x when Stats.max PrimaryVoteShares < 0.5 && (Series.getAt 0 PrimaryVoteShares = x)  -> 1.
                                 | x when Stats.max PrimaryVoteShares < 0.5 && (Series.getAt 1 PrimaryVoteShares = x)  -> 1.
                                 | _ -> 0.
            let RunoffWin = match election.Runoff, election.RunoffCoC with
                            | None, None -> 1.
                            | Some election, Some cand when (election.VoteShares district).[cand.[minority]] > 0.5 -> 1.
                            | _ -> 0.
            let GenWin = match election.General, election.GeneralCoC with
                         | None, None -> 1.
                         | Some election, Some cand when (election.VoteShares district).[cand.[minority]] > 0.5 -> 1.
                         | _ -> 0.
            PrimaryAdvance * RunoffWin * GenWin

    /// <summary>
    /// Compute the group CVAP share of a district.
    /// </summary>
    /// <param name="districtData"> Frame where each row is a precinct within the district and contains
    /// CVAP columns.
    /// </param>
    /// <param name="minority"> With respect to which minority?</param>
    /// <param name="year"> Which year to calculation CVAP share for? </param>
    /// <typeparam name="'a">The type of the Precinct Key index.  Should be string or int</typeparam>
    /// <returns> Minorities CVAP share estimate for year </returns>
    let CVAPShare (districtData: District<'a>) (minority: Minority.Minority) (year: Year) = 
        let _, MinCVAPCol, TotCVAPCol = minority
        let DistrictMinorityCVAP = Stats.sum districtData.[MinCVAPCol.[year]]
        let DistrictTotalCVAP = Stats.sum districtData.[TotCVAPCol.[year]]
        DistrictMinorityCVAP / DistrictTotalCVAP


    /// <summary>
    /// Compute the group alignment of a district; aka twice the minorities CVAP share capped at 1.
    /// </summary>
    /// <param name="districtData"> Frame where each row is a precinct within the district and contains
    /// CVAP columns.
    /// </param>
    /// <param name="minority"> With respect to which minority?</param>
    /// <param name="year"> Which year to calculation alignment for? </param>
    /// <typeparam name="'a">The type of the Precinct Key index.  Should be string or int</typeparam>
    /// <returns> Twice the minorities CVAP share capped at 1. </returns>
    let AlignmentCVAP (districtData: District<'a>) (minority: Minority.Minority) (year: Year) = 
        let DistrictCVAPShare = CVAPShare districtData minority year
        min (2. * DistrictCVAPShare) 1.

    /// Vacuous alignment version, for non-calibrating by CVAP Share.
    let EmptyAlignment (districtData: District<'a>) (minority: Minority.Minority) (year: Year) = 1.


    /// <summary>
    /// Generate ElectionDetails record.
    /// </summary>
    /// /// <param name="minority">The minority group to calculate with respect to</param>
    /// <param name="election"> The election group to generate detials for </param>
    /// <param name="district">DataFrame representing CVAP and Candidate results by precinct for the 
    /// district</param>
    /// <typeparam name="'a">The type of the Precinct Key index.  Should be string or int</typeparam>
    /// <returns>ElectionDetails record</returns>
    let DistrictElectionDetails ((name, minCol, totCol): Minority.Minority) (district: District<'a>)
                                (election: ElectionGroup) = 
        let ElectName = election.Name
        let CoC = election.PrimaryCoC.[name]
        let PrimaryVoteShares = election.Primary.VoteShares district |> Series.sortBy (fun v -> - v)
        let CoCShare = PrimaryVoteShares.[CoC]
        let CoCPlace = PrimaryVoteShares |> Series.filterValues (fun x -> x >= CoCShare) |> Series.countKeys
        let firstPlace = PrimaryVoteShares.GetAt 0
        let numberOfCands = PrimaryVoteShares.KeyCount
        let groupAlignment = CVAPShare district (name, minCol, totCol) election.Year

        let ExistsRunoff, CoCRO, CoCROShare = match election.RunoffCoC, election.Runoff with
                                              | Some cand, Some e -> true, cand.[name], e.VoteShares district |> Series.get cand.[name]
                                              | _ -> false, "", 0.
        let ExistsGen, CoCGen, CoCGenShare = match election.GeneralCoC, election.General with
                                              | Some cand, Some e -> true, cand.[name], e.VoteShares district |> Series.get cand.[name]
                                              | _ -> false, "", 0.
    
        {Name=ElectName; GroupControl=groupAlignment;
         CoC=CoC; CoCPerc=CoCShare; CoCPlace=CoCPlace; FirstPlacePerc=firstPlace; NumCands=numberOfCands;
         ExistsRunoff=ExistsRunoff; CoCRO=CoCRO; CoCPercRO=CoCROShare;
         ExistsGen=ExistsGen; CoCGen=CoCGen;CoCPercGen=CoCGenShare}


    /// <summary>
    /// Returns the VRA Effectiveness Score fo the passed district and minority group
    /// </summary>
    /// <param name="minority">The minority group to calculate with respect to</param>
    /// <param name="elections">The array of election groups to consider</param> 
    /// <param name="successFunc">How success in an election group is defined for the CoC</param> 
    /// <param name="logitParams"> The parameters for the logit function adjustment, if any. </param>
    /// <param name="alignmentYear"> For which years CVAP the alignment score is computed with respect to. </param>
    /// <param name="districtData"> DataFrame representing CVAP and Candidate results by precinct for the 
    /// district </param>
    /// <typeparam name="'a">The type of the Precinct Key index.  Should be string or int</typeparam>
    /// <returns> minority VRA Effectiveness Score for district </returns>
    let DistrictVRAEffectiveness ((name, minCol, totCol): Minority.Minority)
                                 (elections: ElectionGroup array) (successFunc: SuccessFunction<'a>)
                                 (logitParams: LogitParams option) (alignmentYear: Year) alignment (districtData: District<'a>) = 
        let districtAlignment = alignment districtData (name, minCol, totCol) alignmentYear
        let electionWeights = elections |> Array.map (fun (e: ElectionGroup) -> e.Score.[name])
        let minPrefWins = elections |> Array.map (successFunc districtData name)
    
    
        let minVRAscore = Array.map2 (fun w c -> w*c*districtAlignment) electionWeights minPrefWins  
                        |> Array.sum |> (/) <| Array.sum electionWeights
        match logitParams with
        | Some {LogitParams.Coef = coef; LogitParams.Intercept = intercept} -> 1./(1. + exp -(coef*minVRAscore + intercept))
        | None -> minVRAscore


    /// <summary>
    /// VRA Effectiveness Score for each district in the plan across all of the passed minority groups
    /// </summary>
    /// <param name="planData"> Frame containing precinct data and column representing district </param>
    /// <param name="districtCol"> The Column containing district id. </param>
    /// <param name="minorities"> Array of `(Minority.Minority * LogitParams option)` pairs to use for VRA Effectiveness scores</param>
    /// <param name="elections"> Array of election groups to use in score computation </param>
    /// <param name="successFunc"> How success in an election group is defined for the CoC </param>
    /// <param name="alignmentYear"> For which years CVAP the alignment score is computed with respect to. </param>
    /// <typeparam name="'a">The type of the Precinct Key index.  Should be string or int</typeparam>
    /// <typeparam name="'b">The type of the district id.  Should be string or int</typeparam>
    /// <returns> PlanVRAScores type.  Representing VRA Effectiveness scores for each district for each minority group </returns>
    let PlanVRAEffectiveness planData (districtCol: Column) (minorities: (Minority.Minority * LogitParams option) array)
                             (elections: ElectionGroup array) (successFunc: SuccessFunction<'a>) (alignmentYear: Year) alignment
                             : PlanVRAScores<'b> = 
        let districtIDs = planData |> Frame.getCol districtCol |> Series.values |> Seq.distinct
        let districts = districtIDs |> Seq.map (fun d -> planData |> Frame.filterRowsBy districtCol d)
    
        let PlanVRAEffectivenessForMinority (minority: Minority.Minority, logitparams: LogitParams option) = 
            let VRAscores = districts |> Seq.map (DistrictVRAEffectiveness minority elections successFunc logitparams alignmentYear alignment)
            Seq.zip districtIDs VRAscores |> Map
    
        let minorityNames = minorities |> Array.map ((fun (name,_,_) -> name) << fst)
        let minorityScores = minorities |> Array.map PlanVRAEffectivenessForMinority
        Array.zip minorityNames minorityScores |> Map.ofArray


    /// <summary>
    /// VRA Effectiveness Score and election details for each district in the plan across all of the passed
    /// minority groups.
    /// </summary>
    /// <param name="planData"> Frame containing precinct data and column representing district </param>
    /// <param name="districtCol"> The Column containing district id. </param>
    /// <param name="minorities"> Array of `(Minority.Minority * LogitParams option)` pairs to use for VRA Effectiveness scores</param>
    /// <param name="elections"> Array of election groups to use in score computation </param>
    /// <param name="successFunc"> How success in an election group is defined for the CoC </param>
    /// <param name="alignmentYear"> For which years CVAP the alignment score is computed with respect to. </param>
    /// <typeparam name="'a">The type of the Precinct Key index.   Should be string or int</typeparam>
    /// <typeparam name="'b">The type of the district id.  Should be string or int</typeparam>
    /// <returns> PlanVRASummary type.  Representing VRA Effectiveness scores, group control, and election details for each district for each minority group </returns>
    let PlanVRAEffectivenessDetailed planData (districtCol: Column) (minorities: (Minority.Minority * LogitParams option) array)
                                    (elections: ElectionGroup array) (successFunc: SuccessFunction<'a>) (alignmentYear: Year) alignment: PlanVRASummary<'b>= 
        let districtIDs = planData |> Frame.getCol districtCol |> Series.values |> Seq.distinct
        let districts = districtIDs |> Seq.map (fun d -> planData |> Frame.filterRowsBy districtCol d)
    
        let districtDetails (minority: Minority.Minority) (logitparams: LogitParams option) (district: District<'a>) = 
            {
                Score = DistrictVRAEffectiveness minority elections successFunc logitparams alignmentYear alignment district 
                CVAPShare = CVAPShare district minority alignmentYear
                ElectionDetails = elections |> Array.map (DistrictElectionDetails minority district)
            }
    
        let PlanVRAEffectivenessForMinority (minority: Minority.Minority, logitparams: LogitParams option) = 
            let VRAscores = districts |> Seq.map (districtDetails minority logitparams)
            Seq.zip districtIDs VRAscores |> Map
    
        let minorityNames = minorities |> Array.map ((fun (name,_,_) -> name) << fst)
        let minorityScores = minorities |> Array.map PlanVRAEffectivenessForMinority
        Array.zip minorityNames minorityScores |> Map.ofArray