using System;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading;
using Ionic.Zip;

namespace Measurement
{
    public static class ProcessMeasurer
    {
        /// <summary>
        /// Starts and measures performance of an executable file or a script.
        /// </summary>
        /// <param name="fileName">An executable file name or cmd file or bat or zip file.</param>
        /// <param name="arguments"></param>
        /// <param name="timeout"></param>
        /// <param name="memoryLimit">Maximum allowed memory use for the process (bytes).</param>
        /// <param name="outputLimit">Maximum length of the process standard output stream (characters).</param>
        /// <param name="errorLimit">Maximum length of the process standard error stream (characters).</param>
        /// <returns></returns>
        public static ProcessRunMeasure Measure(string fileName, string arguments, TimeSpan timeout, long? memoryLimit = null, long? outputLimit = null, long? errorLimit = null)
        {
            var stdOut = new MemoryStream();
            var stdErr = new MemoryStream();
            StreamWriter out_writer = new StreamWriter(stdOut);
            StreamWriter err_writer = new StreamWriter(stdErr);
            long out_lim = outputLimit ?? long.MaxValue;
            long err_lim = errorLimit ?? long.MaxValue;

            bool isZip = ZipFile.IsZipFile(fileName);

            string localFileName = null, tempFolder = null;
            if (isZip)
            {
                tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempFolder);
                localFileName = ExtractZip(fileName, tempFolder);
            }
            else
                localFileName = fileName;

            Process p = StartProcess(localFileName, arguments,
                output => WriteToStream(output, out_writer, ref out_lim),
                error => WriteToStream(error, err_writer, ref err_lim));

            long maxmem = 0L;
            bool exhausted_time = false, exhausted_memory = false;

            try
            {
                do
                {
                    p.Refresh();
                    if (!p.HasExited)
                    {
                        long m = Memory(p);
                        maxmem = Math.Max(maxmem, m);

                        TimeSpan wc = DateTime.Now - p.StartTime;
                        if (wc >= timeout)
                        {
                            Trace.WriteLine("Process timed out; killing.");
                            exhausted_time = true;
                            Kill(p);
                        }
                        else if (memoryLimit.HasValue && m > memoryLimit.Value)
                        {
                            Trace.WriteLine("Process uses too much memory; killing.");
                            exhausted_memory = true;
                            Kill(p);
                        }
                        else if (out_lim <= 0 || err_lim <= 0)
                        {
                            Trace.WriteLine("Process produced too much output; killing.");
                            Kill(p);
                            throw new Exception("Process produced too much output.");
                        }
                    }
                }
                while (!p.WaitForExit(500));
            }
            catch (InvalidOperationException ex)
            {
                Trace.WriteLine("Invalid Operation: " + ex.Message);
                Trace.WriteLine("Assuming process has ended.");
            }

            p.WaitForExit();

            maxmem = Math.Max(maxmem, Memory(p));
            TimeSpan processorTime = exhausted_time ? timeout : p.TotalProcessorTime;
            TimeSpan wallClockTime = exhausted_time ? timeout : (DateTime.Now - p.StartTime);
            int exitCode = p.ExitCode;        

            p.Close();

            Thread.Sleep(500); // Give the asynch stdout/stderr events a chance to finish.
            out_writer.Flush();
            err_writer.Flush();

            stdOut.Seek(0, SeekOrigin.Begin);
            stdErr.Seek(0, SeekOrigin.Begin);

            if (isZip)
            {
                Directory.Delete(tempFolder, true);
            }

            return new ProcessRunMeasure(
                processorTime,
                wallClockTime,
                maxmem,
                exhausted_time ? Measurement.Measure.CompletionStatus.Timeout : 
                    exhausted_memory ? Measurement.Measure.CompletionStatus.OutOfMemory :
                        exitCode != 0 && exitCode != 1 ? Measurement.Measure.CompletionStatus.Error : // For F*, a return value of 1 is still ok.
                            Measurement.Measure.CompletionStatus.Success,
                exitCode,
                stdOut,
                stdErr);
        }

        private static string[] ExecutableExtensions = new string[] { "exe" };
        private static string[] BatchFileExtensions = new string[] { "bat", "cmd" };

        private static string CreateFilenameFromUri(Uri uri)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(uri.OriginalString.Length);
            foreach (char c in uri.OriginalString)
            {
                sb.Append(Array.IndexOf(invalidChars, c) < 0 ? c : '_');
            }
            return sb.ToString();
        }

        private static void CopyStream(Stream source, Stream target)
        {
            const int bufSize = 0x1000;
            byte[] buf = new byte[bufSize];
            int bytesRead = 0;
            while ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
                target.Write(buf, 0, bytesRead);
        }

        /// <summary>
        /// Extracts contents of zip archive to target directory and finds main executable within it.
        /// </summary>
        /// <param name="fileName">Name of zip file</param>
        /// <param name="targetFolder">Foler to extract archive's contents to</param>
        /// <returns>Name of the main executable</returns>
        private static string ExtractZip(string fileName, string targetFolder)
        {
            string localExecutable = null;
            int execCount = 0;

            using (var zip = ZipFile.Read(fileName))
            {
                foreach (var fn in zip.EntryFileNames)
                {
                    var ext = Path.GetExtension(fn).Substring(1);
                    if (ExecutableExtensions.Contains(ext) || BatchFileExtensions.Contains(ext))
                    {
                        ++execCount;
                        localExecutable = Path.Combine(targetFolder, fn);
                    }
                }
                if (execCount == 1)
                {
                    // If zip contains exactly one executable, then it is the one we need.
                    zip.ExtractAll(targetFolder);
                    return localExecutable;
                }
            }

            localExecutable = null;

            // If single executable expectation failed, try to treat zip as a package with main executable name stored within a relationship
            using (Package pkg = Package.Open(fileName, FileMode.Open))
            {
                PackageRelationshipCollection rels = pkg.GetRelationships();
                var relsCount = rels.Count();

                if (relsCount != 1)
                    throw new Exception("Single executable expectation is failed, when interpreting archive as a package relationships appeared incorrect.");

                PackageRelationship main = rels.First();

                var parts = pkg.GetParts();
                foreach (PackagePart part in parts)
                {
                    using (Stream s = part.GetStream(FileMode.Open, FileAccess.Read))
                    {
                        string fn = CreateFilenameFromUri(part.Uri).Substring(1);
                        string targetPath = Path.Combine(targetFolder, fn);
                        using (var fs = new FileStream(targetPath, FileMode.OpenOrCreate))
                        {
                            CopyStream(s, fs);
                        }
                        
                        if (part.Uri == main.TargetUri)
                            localExecutable = targetPath;
                    }
                }
            }

            if (localExecutable == null)
                throw new Exception("Main executable not found in zip.");

            return localExecutable;
        }

        private static Process StartProcess(string fileName, string arguments, Action<string> stdOut, Action<string> stdErr)
        {
            Process p = new Process();
            p.StartInfo.FileName = fileName;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.UseShellExecute = false;
            p.OutputDataReceived += (sender, args) => { if (args != null && args.Data != null) stdOut(args.Data); };
            p.ErrorDataReceived += (sender, args) => { if (args != null && args.Data != null) stdErr(args.Data); };
            
            if (fileName.EndsWith(".cmd") || fileName.EndsWith(".bat"))
            {
                p.StartInfo.FileName = System.IO.Path.Combine(Environment.SystemDirectory, "cmd.exe");
                p.StartInfo.Arguments = "/c " + fileName + " " + p.StartInfo.Arguments;
            }

            bool retry;
            do
            {
                retry = false;
                try
                {
                    p.Start();
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    if (ex.Message == "The process cannot access the file because it is being used by another process")
                    {
                        Trace.WriteLine("Retrying to execute command...");
                        Thread.Sleep(500);
                        retry = true;
                    }
                    else throw ex;
                }
            } while (retry);

            try
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.ProcessorAffinity = (IntPtr)1L;
                p.PriorityClass = ProcessPriorityClass.RealTime;
            }
            catch (InvalidOperationException ex)
            {
                if (!(ex.Message.Contains("Cannot process request because the process") && ex.Message.Contains("has exited")))
                    throw;
            }

            return p;
        }

        private static void Kill(Process p)
        {
            bool retry;
            do
            {
                retry = false;
                try
                {
                    if (!p.HasExited) p.Kill();
                    //foreach (Process cp in Process.GetProcessesByName(p.ProcessName))
                    //    if(!cp.HasExited) cp.Kill();
                }
                catch
                {
                    Thread.Sleep(100);
                    retry = true; // could be access denied or similar
                }
            } while (retry);
        }

        private static void WriteToStream(string text, StreamWriter stream, ref long limit)
        {
            try
            {
                if (limit > 0 && text != null)
                {
                    stream.WriteLine(text);
                    limit -= text.Length;
                }
            }
            catch (System.NullReferenceException)
            {
                // That's okay, let's just discard the output.
            }
        }

        private static long Memory(Process p)
        {
            long r = 0;

            //foreach (Process cp in Process.GetProcessesByName(p.ProcessName))
            //    try { r += cp.PeakVirtualMemorySize64; } catch { /* OK */ }
            try
            {
                if (!p.HasExited)
                    r = p.PeakVirtualMemorySize64;
            }
            catch
            {
                // OK because the process has a chance to exit.
            }

            return r;
        }

        private static TimeSpan Time(Process p, bool wallclock)
        {
            if (wallclock)
                return DateTime.Now - p.StartTime;
            else
                return p.TotalProcessorTime;
        }

    }
}
