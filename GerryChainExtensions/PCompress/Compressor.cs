using System;
//using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
//using System.Linq;
using System.Text;
using GerryChain;

namespace PCompress
{
    public class Recorder
    {
        public Chain MarkovChain { get; init; }
        public string FileName { get; init; }

        public string Executable { get; init; }

        public int Threads { get; init; }

        /// <summary>
        /// Returns a new Recorder class instance
        /// </summary>
        /// <param name="chain"> Instance of chain class to record. </param>
        /// <param name="fileName"> File name to save to. </param>
        /// <param name="executable"> Path to executable to run pcompressor </param>
        /// <param name="threads"> How many threads to use in the recording. </param>
        /// <remarks> TODO:: Do I want to use logical cores as default (Environment.ProcessorCount)
        /// or someother system based measure of parallelizability? </remarks>
        public Recorder(Chain chain, string fileName, string executable = "pcompress -e", int threads = 0)
        {
            MarkovChain = chain;
            FileName = fileName;
            Executable = executable;

            if (threads > 0)
            {
                Threads = threads;
            }
            else
            {
                Threads = Environment.ProcessorCount;
            }
        }

        public void Record()
        {
            var executingAssembly = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var executingAssemblyDir = Path.GetDirectoryName(executingAssembly);
            
            using (Process process = new Process())
            {
                var outputStream = new StreamWriter($"{ Environment.CurrentDirectory}/{FileName}");
                process.StartInfo.FileName = "zsh";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.StandardInputEncoding = Encoding.UTF8;
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.Arguments = $"-c pcompress -e | xz -e -T {Threads}";
                process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        outputStream.WriteLine(e.Data);
                    }
                });

                process.Start();
                process.BeginOutputReadLine();

                StreamReader myStreamReader = process.StandardError;
                StreamWriter writer = new StreamWriter($"{ Environment.CurrentDirectory}/test_output.txt"); //process.StandardInput;
                
                foreach (Partition p in MarkovChain)
                {
                    writer.WriteLine("[{0}]", string.Join(", ", p.Assignments));
                    // Console.WriteLine(p.SelfLoops);
                }
                writer.Close();

                Console.WriteLine(myStreamReader.ReadToEnd());

                process.WaitForExit();
                // Free resources associated with process.
                process.Close();
                outputStream.Close();
            }
        }
    }

    // public class Replayer : IEnumerable<Partition>
    // {
    //     IEnumerator IEnumerable.GetEnumerator()
    //     {
    //         return GetEnumerator();
    //     }

    //     public IEnumerator<Partition> GetEnumerator()
    //     {
    //         return new ChainReplayer(this);
    //     }

    //     public class ChainReplayer : IEnumerator<Partition>
    //     {

    //     }
    // }
}