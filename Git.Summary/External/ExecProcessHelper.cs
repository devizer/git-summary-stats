using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Universe
{
    public static class ExecProcessHelper
    {
        public class ExecResult
        {
            public int ExitCode { get; set; }
            public string ErrorText { get; set; }
            public string OutputText { get; set; }
            public Exception OutputException { get; set; }
            public Exception ErrorException { get; set; }
            public bool IsTimeout { get; set; }
            public int MillisecondsTimeout { get; set; }

            public void DemandGenericSuccess(string operationDescription, bool failOnStderr = false)
            {
                bool isFail =
                    IsTimeout
                    || ExitCode != 0
                    || ErrorException != null
                    || OutputException != null
                    || failOnStderr && !string.IsNullOrEmpty(ErrorText);

                if (isFail)
                {
                    StringBuilder reason = new StringBuilder();
                    if (IsTimeout) reason.AppendLine($"  - Operation canceled by timeout ({(MillisecondsTimeout > 0 ? $"{MillisecondsTimeout:n0} milliseconds" : "infinite")})");
                    if (ExitCode != 0) reason.AppendLine($"  - Exit code is {ExitCode}");
                    if (!string.IsNullOrEmpty(ErrorText)) reason.AppendLine($"  - Std Error: {ErrorText}");
                    if (OutputException != null) reason.AppendLine($"  - Output stream exception {OutputException}");
                    if (ErrorException != null) reason.AppendLine($"  - Error stream exception {ErrorException}");

                    throw new ProcessInvocationException(
                        $"{operationDescription} failed. The reason is:{Environment.NewLine}{reason}",
                        ExitCode,
                        ErrorText);
                }
            }
        }

        public static ExecResult HiddenExec(string command, string args, string workingFolder = null, int millisecondsTimeout = -1, IDictionary<string, string> environment = null, string standardInputText = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(command, args)
            {
                // CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
            };

            if (!string.IsNullOrEmpty(workingFolder))
                startInfo.WorkingDirectory = workingFolder;

            if (standardInputText != null)
            {
                startInfo.RedirectStandardInput = true;
#if !NETFRAMEWORK && !NETCOREAPP2_0 && !NETCOREAPP1_1 && false
                // is not used
                startInfo.StandardInputEncoding = Encoding.UTF8;
#endif
            }

#if NETFRAMEWORK
            var envDictionary = startInfo.EnvironmentVariables;
#else
            var envDictionary = startInfo.Environment;
#endif
            envDictionary["LANG"] = "C";
            envDictionary["LC_ALL"] = "C";
            if (environment != null)
                foreach (var pair in environment)
                    envDictionary[pair.Key] = pair.Value;

            Process process = new Process()
            {
                StartInfo = startInfo,
            };

            ManualResetEventSlim outputDone = new ManualResetEventSlim(false);
            ManualResetEventSlim errorDone = new ManualResetEventSlim(false);
            string outputText = null;
            StringBuilder outputTextBuilder = new StringBuilder();
            string errorText = null;
            StringBuilder errorTextBuilder = new StringBuilder();
            Exception outputException = null;
            Exception errorException = null;

            using (process)
            {
                process.Start();
                if (!string.IsNullOrEmpty(standardInputText))
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        StringReader rdr = new StringReader(standardInputText);
                        string inputLine;
                        using (StreamWriter inputWriter = process.StandardInput)
                            while ((inputLine = rdr.ReadLine()) != null)
                                inputWriter.WriteLine(inputLine);
                    });
                }

                // void 

                ManualResetEventSlim startedErrorReader = new ManualResetEventSlim(false);
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    startedErrorReader.Set();
                    try
                    {
                        ReadLineByLine(errorTextBuilder, process.StandardError);
                        // errorText = errorTextBuilder.ToString();
                        // errorText = process.StandardError.ReadToEnd();
                    }
                    catch (Exception ex)
                    {
                        errorException = ex;
                    }
                    finally
                    {
                        errorDone.Set();
                    }
                }
                );

                ManualResetEventSlim startedOutputReader = new ManualResetEventSlim(false);
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    startedOutputReader.Set();
                    try
                    {
                        ReadLineByLine(outputTextBuilder, process.StandardOutput);
                        // outputText = outputTextBuilder.ToString();
                        // outputText = process.StandardOutput.ReadToEnd();
                    }
                    catch (Exception ex)
                    {
                        outputException = ex;
                    }
                    finally
                    {
                        outputDone.Set();
                    }
                }
                );

                WaitHandle.WaitAll(new[] { startedOutputReader.WaitHandle, startedErrorReader.WaitHandle });
                Stopwatch startAt = Stopwatch.StartNew();
                bool isProcessFinished = process.WaitForExit(millisecondsTimeout);

                int remainingMilliseconds = millisecondsTimeout - (int)startAt.ElapsedMilliseconds;

                if (args.EndsWith("start", StringComparison.CurrentCultureIgnoreCase))
                    if (Debugger.IsAttached) Debugger.Break();

                if (isProcessFinished) remainingMilliseconds = 1;

                bool isSuccess1 = WaitHandle.WaitAll(
                    new[] { errorDone.WaitHandle, outputDone.WaitHandle },
                    Math.Max(1, remainingMilliseconds));

                bool isSuccess = isProcessFinished;

                var exitCode = isSuccess ? process.ExitCode : -1;

                // System.ArgumentOutOfRangeException : Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'chunkLength')
                // But locks are not suitable
                errorText = TryStreamAndRetry(errorTextBuilder);
                outputText = TryStreamAndRetry(outputTextBuilder);
                errorText = errorText?.TrimEnd('\r', '\n');
                return new ExecResult()
                {
                    IsTimeout = !isSuccess,
                    ExitCode = exitCode,
                    OutputText = outputText,
                    ErrorText = errorText,
                    OutputException = outputException,
                    ErrorException = errorException,
                    MillisecondsTimeout = millisecondsTimeout,
                };
            }
        }

        private static void ReadLineByLine(StringBuilder result, StreamReader source)
        {
            while (true)
            {
                var line = source.ReadLine();
                if (line == null) break;
                result.AppendLine(line);
            }
        }

        static string TryStreamAndRetry(StringBuilder stringBuilder, int retryCount = 3)
        {
            for (int i = retryCount; i > 0; i--)
            {
                try
                {
                    return stringBuilder.ToString();
                }
                catch (Exception ex)
                {
                    if (i == 1) throw;
                    Thread.Sleep(0);
                }
            }

            throw new InvalidOperationException("Never goes here");
        }
    }

    public class ProcessInvocationException : Exception
    {
        public int ExitCode { get; set; }
        public string ErrorText { get; set; }

        public ProcessInvocationException(string message, int exitCode, string errorText) : base(message)
        {
            ExitCode = exitCode;
            ErrorText = errorText;
        }
    }
}