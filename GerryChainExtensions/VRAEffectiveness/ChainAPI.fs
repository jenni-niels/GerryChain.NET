namespace VRA

open FSharp.Data
open Deedle
open VRAEffectiveness

type AllignmentType =
    | None = 0
    | CVAP = 1

type VRAAPI(state: string, alignmentType : AllignmentType) = 
    let (+/) path path' = System.IO.Path.Combine(path, path')
    let PathCombine path path' path'' = System.IO.Path.Combine(path, path', path'')

    let StateSuccessFunction: Map<string, SuccessFunction<int>> = Map.ofArray [|"texas", CoCCarriesElectTX; 
                                                                                "louisiana", CoCCarriesElectLA;
                                                                                "massachusetts", CoCCarriesElectPlurality|]

    let StateAlignmentOptions: Map<AllignmentType, AlignmentFunction<int>> = Map.ofArray [|AllignmentType.None, EmptyAlignment; AllignmentType.CVAP, AlignmentCVAP|]

    let alignment = match StateAlignmentOptions |> Map.tryFind alignmentType with
                    | Some func -> func
                    | None -> EmptyAlignment

    let JsonFile = sprintf "%s.json" <| state |> PathCombine VRAAPI.executingAssemblyDir "resources"
    let CsvFile = sprintf "%s.csv" <| state |> PathCombine VRAAPI.executingAssemblyDir "resources"
    let JsonValue = JsonValue.Load(JsonFile)
    let StateData = Frame.ReadCsv(CsvFile) //|> Frame.indexRowsString PrecinctID

    let VRAparser = Parser JsonValue
    let Minorities = VRAparser.Minorities
    let Elections = VRAparser.Elections
    let AlignmentYear = VRAparser.AlignmentYear
    let CoCSuccess = StateSuccessFunction.[state]

    static member executingAssembly = System.Reflection.Assembly.GetExecutingAssembly().Location
    static member executingAssemblyDir = System.IO.Path.GetDirectoryName VRAAPI.executingAssembly

    // member this.stateName = state

    member this.Invoke (assignment: int array): Map<Minority.Name, float array> =
        let districtID = "District"
        let PlanData = StateData |> Frame.addCol districtID (assignment |> Array.mapi(fun i d -> (i, d)) |> Series.ofObservations)
        let vrascores: PlanVRASummary<int> = PlanVRAEffectivenessDetailed PlanData districtID Minorities Elections CoCSuccess AlignmentYear alignment
        vrascores |> Map.map (fun _ scores -> scores |> Map.map (fun _ s -> s.Score) |> Map.toSeq |> Seq.map snd |> Seq.toArray)