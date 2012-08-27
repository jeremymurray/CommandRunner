using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using NDesk.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Runs a command and can capture or modify its console output. 
/// </summary>
class CommandRunner
{
    static int Main(string[] args)
    {
        List<string> ruleFiles = new List<string>();
        List<string> commands = new List<string>();
        List<string> commandFiles = new List<string>();
        List<string> searchReplaceFiles = new List<string>();
        var outputHandler = new OutputHandler ();
        
        bool timeRun = false;
        bool statistics = false;
        bool report = false;
        bool rulesOnly = false;
        bool searchReplaceOnly = false;
        bool showHelp = false;
        bool showExampleRule = false;
        bool showExampleSearchReplace = false;

        bool stopOnFirstError = false;
        bool sumReturnValues = false;
        
        bool forceReturn = false;
        int forceReturnValue = 0;
        
        bool passedRuleExists = false;
        OutputRule passedRule = new OutputRule();
        
        var options = new OptionSet () {
            { "c=|command=", "Command to run", arg => commands.Add(arg)},
            { "cf=|commandfile=", "Path to text file of commands to run", arg => commandFiles.Add(arg)},
            { "fr=|forcereturn=", "Sets the return value explicitly, ignoring any command returns", arg => {
                    forceReturn = arg != null;
                    if(!int.TryParse(arg, out forceReturnValue))
                    {
                        Console.WriteLine ("ERROR - Error in options - cannot convert passed return value to integer: " + arg);
                        Environment.Exit(1);
                    }
                }
            },
                
            { "h|?|help", "Shows help and quits", arg => showHelp = arg != null },
            { "mp|matchpass", "Allows or disallows a matched rule from passing the original output line", arg => outputHandler.outputMatchPass = arg != null},
            { "mse|matchstderr", "Allows or disallows a matched rule from printing its stderr output", arg => outputHandler.outputMatchStderr = arg != null},
            { "mso|matchstdout", "Allows or disallows a matched rule from printing its stdout output", arg => outputHandler.outputMatchStdout = arg != null},
            { "p|pass", "Allows or disallows passing the original output line if it did not match any rules", arg => outputHandler.outputMissPass = arg != null},
            { "printrules", "Reads all rules, prints them in input file format, and quits", arg => rulesOnly = arg != null},
            { "printexamplerule", "Prints an example rule in input file format and quits", arg => showExampleRule = arg != null},
            { "printexamplesearchreplace", "Reads all search/replace pairs, prints them in an input file format, and quits", arg => showExampleSearchReplace = arg != null},
            { "q|quiet", "Turns off most stadard output, including: unmatched pass, match pass, match stdout, match stderr", arg => {
                    outputHandler.outputMissPass = arg == null;
                    outputHandler.outputMatchPass = arg == null;
                    outputHandler.outputMatchStdout = arg == null;
                    outputHandler.outputMatchStderr = arg == null;
                }
            },
            { "r|report", "Prints report output of any matched rules at the end of the command run", arg => report = arg != null},
            { "rf=|rulefile=", "Path to file of rules to match", arg => ruleFiles.Add(arg)},
            { "rme|rulematcherror", "Passed rule - allows or disallows forcing a non-zero return for the command on match", arg => {
                    passedRuleExists = true;
                    passedRule.setError = arg != null;
                }
            },
            { "rmp|rulematchpass", "Passed rule - allows or disallows passing original output line on match", arg => {
                    passedRuleExists = true;
                    passedRule.passOutput = arg != null;
                }
            },
            { "rms|rulematchstop", "Passed rule - allows or disallows stopping further rule processing on match", arg => {
                    passedRuleExists = true;
                    passedRule.stopProcessing = arg != null;
                }
            },
            { "rp=|rulepattern=", "Passed rule - regex pattern to match against", arg => {
                    passedRuleExists = true;
                    passedRule.pattern = arg;
                }
            },
            { "rpse|ruleprocessstderr", "Passed rule - allows or disallows processing stderr output lines", arg => {
                    passedRuleExists = true;
                    passedRule.processStdErr = arg != null;
                }
            },
            { "rpso|ruleprocessstdout", "Passed rule - allows or disallows processing stdout output lines", arg => {
                    passedRuleExists = true;
                    passedRule.processStdOut = arg != null;
                }
            },
            { "rr=|rulereport=", "Passed rule - report output format on match", arg => {
                    passedRuleExists = true;
                    passedRule.reportFormat = arg;
                }
            },
            { "rse=|rulestderr=", "Passed rule - stderr output format on match", arg => {
                    passedRuleExists = true;
                    passedRule.stderrFormat = arg;
                }
            },
            { "rso=|rulestdout=", "Passed rule - stdout output format on match", arg => {
                    passedRuleExists = true;
                    passedRule.stdoutFormat = arg;
                }
            },
            { "rsp=|ruleshortpattern=", "Passed rule - short regex pattern to match against first for expensive rule patterns", arg => {
                    passedRuleExists = true;
                    passedRule.shortPattern = arg;
                }
            },
            { "s|stats", "Shows statistics for rules", arg => statistics = arg != null},
            { "srf=|searchreplacefile=", "Path to file of search/replace pairs", arg => searchReplaceFiles.Add(arg)},
            { "stoponerror", "When running multiple commands, stop execution after first error", arg => stopOnFirstError = arg != null},
            { "sumreturns", "When running multiple commands, set final return value to sum of all subcommand return values", arg => sumReturnValues = arg != null},
            { "t|time", "Shows processing time", arg => timeRun = arg != null},
        };

        string[] otherArgs = null;
        
        try
        {
            otherArgs = options.Parse(args).ToArray();
        }
        catch (NDesk.Options.OptionException e)
        {
            Console.WriteLine ("ERROR - Error in options - " + e.Message);
            Environment.Exit(1);
        }
        catch (System.ArgumentNullException e)
        {
            Console.WriteLine ("ERROR - Error in options - " + e.Message);
            Environment.Exit(1);
        }
        
        if (showHelp)
        {
            ShowHelp (options);
            Environment.Exit(0);
        }
        if (showExampleRule)
        {
            OutputRule exampleRule = new OutputRule();
            exampleRule.title = "Example Report Rule";
            exampleRule.description = "A rule that matches an input string from dir, shows a shortPattern to reduce misses on expensive patterns, shows parsing, and creates a report entry.";
            exampleRule.example = @" Directory of D:\projects\CommandRunner";
            exampleRule.pattern = "^ Directory of ([a-zA-Z]):(.+)$";
            exampleRule.shortPattern = "Directory";
            exampleRule.reportFormat = "Drive: {1}\nFolder: {2}";
            exampleRule.stopProcessing = false;
            OutputRule exampleRule2 = new OutputRule();
            exampleRule2.title = "Example Error Rule";
            exampleRule2.description = "A rule that matches an input string from dir and marks the command as an error (making sure the command returns a non-zero value) if run on drive D.";
            exampleRule2.example = @" Directory of D:\projects\CommandRunner";
            exampleRule2.pattern = "^ Directory of D:.+$";
            exampleRule2.stderrFormat = "\n**********\nERROR - This was run on drive D.\n**********\n";
            exampleRule2.stopProcessing = true;
            exampleRule2.setError = true;
            List<OutputRule> exampleRuleList = new List<OutputRule>(){exampleRule, exampleRule2};
            Console.Write (JsonConvert.SerializeObject(exampleRuleList, Formatting.Indented));
            Environment.Exit(0);
        }
        if (showExampleSearchReplace)
        {
            Dictionary<string, OrderedDictionary> exampleReplacements = new Dictionary<string, OrderedDictionary>();

            exampleReplacements["html_reserved"] = new OrderedDictionary {
                {"$", "%24"},
                {"&", "%26"},
                {"+", "%2B"},
                {",", "%2C"},
                {"/", "%2F"},
                {":", "%3A"},
                {";", "%3B"},
                {"=", "%3D"},
                {"?", "%3F"},
                {"@", "%40"},
            };

            exampleReplacements["html_unsafe"] = new OrderedDictionary {
                {"%", "%25"},
                {"<", "%3C"},
                {">", "%3E"},
                {" ", "%20"},
                {"#", "%23"},
                {"{", "%7B"},
                {"}", "%7D"},
                {"|", "%7C"},
                {"\\", "%5C"},
                {"^", "%5E"},
                {"~", "%7E"},
                {"[", "%5B"},
                {"]", "%5D"},
                {"`", "%60"},
            };
            Console.Write (JsonConvert.SerializeObject(exampleReplacements, Formatting.Indented));
            Environment.Exit(0);
        }

        if (passedRuleExists)
        {
            if (!passedRule.IsValid())
            {
                Console.WriteLine ("ERROR - Passed rule is invalid:");
                Console.Write (JsonConvert.SerializeObject(passedRule, Formatting.Indented));
                Environment.Exit(1);
            }
            outputHandler.rules.Add(passedRule);
        }
		
        if (commandFiles.Count != 0)
        {
            foreach (var commandFile in commandFiles)
            {
                if (!System.IO.File.Exists(commandFile))
                {
                    Console.WriteLine ("ERROR - Cannot find command file: " + commandFile);
                    Environment.Exit(1);
                }
                
                using (StreamReader sr = new StreamReader(commandFile)) 
                {
                    while (sr.Peek() >= 0) 
                    {
                        commands.Add(sr.ReadLine());
                    }
                }
            }
        }
                
        if (otherArgs.Length > 0)
        {
            commands.Add(String.Join(" ", otherArgs));
        }
        
        foreach (var ruleFile in ruleFiles)
        {
            if (!System.IO.File.Exists(ruleFile))
            {
                Console.WriteLine ("ERROR - Cannot find rule file: " + ruleFile);
                Environment.Exit(1);
            }
            
            using (StreamReader sr = new StreamReader(ruleFile))
            {
                try {
                    OutputRule[] rules = JsonConvert.DeserializeObject<OutputRule[]>(sr.ReadToEnd());
                    foreach (var rule in rules)
                    {
                        if (!rule.IsValid())
                        {
                            Console.WriteLine ("ERROR - Rule is invalid in rule file: " + ruleFile);
                            Console.Write (JsonConvert.SerializeObject(rule, Formatting.Indented));
                            Environment.Exit(1);
                        }
                        outputHandler.rules.Add(rule);
                    }
                }
                catch (Newtonsoft.Json.JsonSerializationException e)
                {
                    Console.WriteLine ("ERROR - Cannot parse rule file: " + ruleFile + " - " + e.Message);
                    Environment.Exit(1);
                }
                catch (Newtonsoft.Json.JsonReaderException e)
                {
                    Console.WriteLine ("ERROR - Cannot parse rule file: " + ruleFile + " - " + e.Message);
                    Environment.Exit(1);
                }
            }
        }

        foreach (string searchReplaceFile in searchReplaceFiles)
        {
            if (!System.IO.File.Exists(searchReplaceFile))
            {
                Console.WriteLine ("ERROR - Cannot find search/replace file: " + searchReplaceFile);
                Environment.Exit(1);
            }

            using (StreamReader sr = new StreamReader(searchReplaceFile))
            {
                try {
                    Dictionary<string, OrderedDictionary> replacements = JsonConvert.DeserializeObject<Dictionary<string, OrderedDictionary>>(sr.ReadToEnd());
                    foreach (var replacementKey in replacements.Keys)
                    {
                        // if (outputHandler.stringFunctionFormatter.replacements.ContainsKey(replacementKey))
                        // {
                            // Replacing search/replace dictionary.
                            // This is not currently an error, nor can it be easily logged without affecting output.
                        // }
                        outputHandler.stringFunctionFormatter.replacements[replacementKey] = replacements[replacementKey];
                    }
                }
                catch (Newtonsoft.Json.JsonSerializationException e)
                {
                    Console.WriteLine ("ERROR - Cannot parse search/replace file: " + searchReplaceFile + " - " + e.Message);
                    Environment.Exit(1);
                }
                catch (Newtonsoft.Json.JsonReaderException e)
                {
                    Console.WriteLine ("ERROR - Cannot parse search/replace file: " + searchReplaceFile + " - " + e.Message);
                    Environment.Exit(1);
                }
            }
        }
        
        if (rulesOnly)
        {
            Console.WriteLine (JsonConvert.SerializeObject(outputHandler.rules, Formatting.Indented));
            Environment.Exit(0);
        }

        if (searchReplaceOnly)
        {
            Console.WriteLine (JsonConvert.SerializeObject(outputHandler.stringFunctionFormatter.replacements, Formatting.Indented));
            Environment.Exit(0);
        }

        int returnInt = 0;
        
        DateTime start = DateTime.Now;
        foreach (var commandLine in commands)
        {
            int commandReturnInt = RunCommandWithOutputHandler (commandLine, ".", outputHandler);
            if (sumReturnValues)
            {
                returnInt += commandReturnInt;
            }
            else
            {
                returnInt = commandReturnInt;
            }
            
            if (stopOnFirstError && (returnInt > 0 || outputHandler.error))
            {
                break;
            }
        }
        TimeSpan timeSpan = DateTime.Now - start;
        
        if (report)
        {
            outputHandler.Report();
        }
        
        if (statistics)
        {
            Console.Write(outputHandler.Statistics());
        }
        if (timeRun)
        {
            Console.WriteLine("Time elapsed: " + timeSpan);
        }
        
        if (returnInt == 0 && outputHandler.error)
        {
            returnInt = 1;
        }
        
        if (forceReturn)
        {
            returnInt = forceReturnValue;
        }
        
        return returnInt;
    }
    
    static List<OutputRule> ParseRuleFile (string ruleFilePath)
    {
        var rules = new List<OutputRule> ();
        return rules;
    }
    
    static void ShowHelp (OptionSet p)
    {
        Console.WriteLine ("Usage: cr [OPTIONS]+ [command]");
        Console.WriteLine ("Run commands and inspect/replace their standard and error output.");
        Console.WriteLine ();
        Console.WriteLine ("Options:");
        p.WriteOptionDescriptions (Console.Out);
        Environment.Exit(0);
    }
    
    public static int RunCommandWithOutputHandler (string commandLine, string startingDirectory, OutputHandler outputHandler)
    {
        var subProcess = new Process();

        // Windows
        // This will need additional setups for other platforms.
        subProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            // mode is used to redefine the command window width, so there will be no arbitrary linewrap.
            Arguments = "/c mode 1000,50&&" + commandLine,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            WorkingDirectory = startingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        subProcess.OutputDataReceived += outputHandler.HandleOutput;
        subProcess.ErrorDataReceived += outputHandler.HandleErrorOutput;
        
        bool error = false;
        
        try
        {
            subProcess.Start ();
            subProcess.BeginOutputReadLine ();
            subProcess.BeginErrorReadLine ();
        }
        catch (Exception e)
        {
            Console.WriteLine (string.Format ("Could not complete subprocess: {0} with exception:{1}", commandLine, e));
            error = true;
        }
        finally
        {
            subProcess.WaitForExit();
        }

        if (error)
        {
            return 1;
        }
        
        // If we have flagged an error via a rule, pass through any non-zero return value
        // or return 1 if the process returned 0.
        if (outputHandler.error && subProcess.ExitCode == 0)
        {
            return 1;
        }
        
        return subProcess.ExitCode;
    }
    
    public class OutputHandler
    {
        public StringFunctionFormatter stringFunctionFormatter = new StringFunctionFormatter();
        
        public List<OutputRule> rules = new List<OutputRule>();
        public bool outputMissPass = true;
        public bool outputMatchPass = true;
        public bool outputMatchStdout = true;
        public bool outputMatchStderr = true;
        
        public List<string> report = new List<string>();

        public bool error = false;
        
        public enum InputStream 
        {
            STDOUT,
            STDERR,
        }

        public void HandleOutput (object sendingProcessObject, DataReceivedEventArgs eventArgs)
        {
            ProcessRules(eventArgs.Data, InputStream.STDOUT);
        }

        public void HandleErrorOutput (object sendingProcess, DataReceivedEventArgs eventArgs)
        {
            ProcessRules(eventArgs.Data, InputStream.STDERR);
        }
        
        public void ProcessRules (string line, InputStream inputStream)
        {
            if (string.IsNullOrEmpty(line)) return;

            // If any rule matches, matched will be set to true.
            bool matched = false;
            
            // If any rule matches and passes the output, passed will be set to true.
            // This prevents multiple rules passing the same string and duplicating output.
            bool passed = false;
            
            foreach (OutputRule rule in rules)
            {
                if ((inputStream == InputStream.STDOUT && rule.processStdOut) ||
                    (inputStream == InputStream.STDERR && rule.processStdErr))
                {
                    Match match = rule.RegexMatch(line);

                    if (match == null)
                    {
                        continue;
                    }

                    if (!match.Success)
                    {
                        continue;
                    }

                    // GroupCollection doesn't support linq.
                    // var groups = from x in match.Groups select x.ToString();

                    List<string> groupList = new List<string>();
                        
                    foreach (Group g in match.Groups)
                    {
                        groupList.Add(g.ToString());
                    }
                    
                    var groups = groupList.ToArray();
                
                    matched = true;
                    
                    if (!passed && rule.passOutput && outputMatchPass)
                    {
                        passed = true;
                        PassOutput (line, inputStream);
                    }
                    
                    if (!string.IsNullOrEmpty(rule.stdoutFormat) && outputMatchStdout)
                    {
                        try
                        {
                            Console.WriteLine(string.Format(stringFunctionFormatter, rule.stdoutFormat, groups));
                        }
                        catch (System.FormatException)
                        {
                            Console.WriteLine("Error - stdout format is incorrect");
                            Console.WriteLine (JsonConvert.SerializeObject(rule, Formatting.Indented));
                            Environment.Exit(1);
                        }                    
                    }
                    
                    if (!string.IsNullOrEmpty(rule.stderrFormat) && outputMatchStderr)
                    {
                        try
                        {
                            Console.Error.WriteLine(string.Format(rule.stderrFormat, groups));
                        }
                        catch (System.FormatException)
                        {
                            Console.WriteLine(string.Format("Error - stderr format is incorrect\n  pattern: {0}\n  stderr: {1}", rule.pattern, rule.stderrFormat));
                            Environment.Exit(1);
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(rule.reportFormat))
                    {
                        lock (report)
                        {
                            try
                            {
                                report.Add(string.Format(rule.reportFormat, groups));
                            }
                            catch (System.FormatException)
                            {
                                Console.WriteLine(string.Format("Error - Report format is incorrect\n  pattern: {0}\n  report: {1}", rule.pattern, rule.reportFormat));
                                Environment.Exit(1);
                            }
                        }
                    }
                    
                    if (rule.setError)
                    {
                        error = true;
                    }
                    
                    if (rule.stopProcessing)
                    {
                        break;
                    }
                }
            }
            
            if (!matched && outputMissPass)
            {
                PassOutput (line, inputStream);
            }
        }
        
        private void PassOutput (string line, InputStream inputStream)
        {
            if (inputStream == InputStream.STDOUT)
            {
                Console.WriteLine(line);
            }
            else
            {
                Console.Error.WriteLine(line);
            }
        }
        
        public void Report ()
        {
            foreach (string line in report)
            {
                Console.WriteLine(line);
            }
        }
        
        public string Statistics ()
        {
            StringWriter sr = new StringWriter();
            
            sr.WriteLine(string.Format("Number of Rules: {0}", rules.Count));
            
            long regexMatchCount = 0;
            long regexMissCount = 0;
            long shortRegexMatchCount = 0;
            long shortRegexMissCount = 0;
            TimeSpan shortRegexMatchCumulativeDuration = new TimeSpan();
            TimeSpan shortRegexMissCumulativeDuration = new TimeSpan();
            TimeSpan regexMatchCumulativeDuration = new TimeSpan();
            TimeSpan regexMissCumulativeDuration = new TimeSpan();
            
            foreach (OutputRule rule in rules)
            {
                sr.Write(rule.Statistics());
                regexMatchCount += rule.regexMatchCount;
                regexMissCount += rule.regexMissCount;
                shortRegexMatchCount += rule.shortRegexMatchCount;
                shortRegexMissCount += rule.shortRegexMissCount;	
                shortRegexMatchCumulativeDuration += rule.shortRegexMatchCumulativeDuration;
                shortRegexMissCumulativeDuration += rule.shortRegexMissCumulativeDuration;
                regexMatchCumulativeDuration += rule.regexMatchCumulativeDuration;
                regexMissCumulativeDuration += rule.regexMissCumulativeDuration;
            }

            int maxWidth = (regexMatchCount + regexMissCount + shortRegexMatchCount + shortRegexMissCount).ToString().Length;
            string statsOutputPattern = "{0,-21} {1," + maxWidth + "} Duration {2} (Avg: {3})";

			if (rules.Count > 0)
			{
				sr.WriteLine(string.Format(statsOutputPattern, "Pattern Hits:", regexMatchCount, regexMatchCumulativeDuration, regexMatchCount == 0?new TimeSpan(0):TimeSpan.FromTicks(regexMatchCumulativeDuration.Ticks/regexMatchCount)));
				sr.WriteLine(string.Format(statsOutputPattern, "Pattern Misses:", regexMissCount, regexMissCumulativeDuration, regexMissCount == 0?new TimeSpan(0):TimeSpan.FromTicks(regexMissCumulativeDuration.Ticks/regexMissCount)));
				sr.WriteLine(string.Format(statsOutputPattern, "Pattern Total:", regexMatchCount + regexMissCount, regexMatchCumulativeDuration + regexMissCumulativeDuration, regexMatchCount + regexMissCount == 0?new TimeSpan(0):TimeSpan.FromTicks((regexMatchCumulativeDuration + regexMissCumulativeDuration).Ticks/(regexMatchCount + regexMissCount))));

				sr.WriteLine(string.Format(statsOutputPattern, "Short Pattern Hits:", shortRegexMatchCount, shortRegexMatchCumulativeDuration, shortRegexMatchCount == 0?new TimeSpan(0):TimeSpan.FromTicks(shortRegexMatchCumulativeDuration.Ticks/shortRegexMatchCount)));
				sr.WriteLine(string.Format(statsOutputPattern, "Short Pattern Misses:", shortRegexMissCount, shortRegexMissCumulativeDuration, shortRegexMissCount == 0?new TimeSpan(0):TimeSpan.FromTicks(shortRegexMissCumulativeDuration.Ticks/shortRegexMissCount)));
				sr.WriteLine(string.Format(statsOutputPattern, "Short Pattern Total:", shortRegexMatchCount + shortRegexMissCount, shortRegexMatchCumulativeDuration + shortRegexMissCumulativeDuration, shortRegexMatchCount + shortRegexMissCount == 0?new TimeSpan(0):TimeSpan.FromTicks((shortRegexMatchCumulativeDuration + shortRegexMissCumulativeDuration).Ticks/(shortRegexMatchCount + shortRegexMissCount))));
			}
            
            return sr.ToString();	
        }
    }

    
    // ToDo: Case Insensitive
    [JsonObject(MemberSerialization.OptIn)]
    public class OutputRule
    {
        // A short description of the rule.
        [JsonProperty]
        public string title;

        // A longer description of the rule.
        [JsonProperty]
        public string description;

        // An example string that the parser would match against.
        [JsonProperty]
        public string example;

        // If true, a copy of output will be sent to the same stream it was received on.
        [JsonProperty]
        public bool passOutput=true;
        
        // If true and this rule is matched, no further rules will be attempted.
        [JsonProperty]
        public bool stopProcessing=true;
        
        // If true, if matched will prevent process from returning 0;
        [JsonProperty]
        public bool setError=false;
        
        // If true, this rule will be used to evaluate output from the stream.
        [JsonProperty]
        public bool processStdErr=true;
        [JsonProperty]
        public bool processStdOut=true;
        
        // The short regex pattern to match against.  When set, it will create the regex.
        // This pattern should be simple and faster to match than the full regex, with no groups.
        // Needs error handling for pattern mistakes.
        private string _shortPattern;
        [JsonProperty]
        public string shortPattern
        { 
            get
            {
                return _shortPattern; 
            } 
            set 
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _shortPattern = value;
                    _shortRegex = new Regex(_shortPattern);
                    // Compiling doesn't seem to help speed here.
                    // _shortRegex = new Regex(_shortPattern, RegexOptions.Compiled);
                }
            }
        }
        private Regex _shortRegex = null;
        public Regex shortRegex
        {
            get
            {
                return _shortRegex;
            }
        }
                
        // The regex pattern to match against.  When set, it will create the regex.
        // Needs error handling for pattern mistakes.
        private string _pattern;
        [JsonProperty]
        public string pattern
        { 
            get
            {
                return _pattern; 
            } 
            set 
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _pattern = value;
                    _regex = new Regex(_pattern);
                    // Compiling doesn't seem to help speed here.
                    // _regex = new Regex(_pattern, RegexOptions.Compiled);
                }
            }
        }
        private Regex _regex;
        public Regex regex
        {
            get
            {
                return _regex;
            }
        }

        public int shortRegexMatchCount = 0;
        public int shortRegexMissCount = 0;
        public TimeSpan shortRegexMatchCumulativeDuration = new TimeSpan();
        public TimeSpan shortRegexMissCumulativeDuration = new TimeSpan();
        public int regexMatchCount = 0;
        public int regexMissCount = 0;
        public TimeSpan regexMatchCumulativeDuration = new TimeSpan();
        public TimeSpan regexMissCumulativeDuration = new TimeSpan();

        public Match RegexMatch (string testString)
        {
            if (string.IsNullOrEmpty(testString)) return null;

            if (shortRegex != null)
            {
                DateTime shortRegexMatchStart = DateTime.Now;
                Match shortRegexMatch = shortRegex.Match(testString);
                TimeSpan shortRegexMatchDuration = DateTime.Now - shortRegexMatchStart;
                if (!shortRegexMatch.Success)
                {
                    // Short regex failed so processing can stop early.
                    shortRegexMissCount++;
                    shortRegexMissCumulativeDuration += shortRegexMatchDuration;
                    return shortRegexMatch;
                }
                else
                {
                    shortRegexMatchCount++;
                    shortRegexMatchCumulativeDuration += shortRegexMatchDuration;
                }
            }
            if (regex != null)
            {
                DateTime regexMatchStart = DateTime.Now;
                Match regexMatch = regex.Match(testString);
                TimeSpan regexMatchDuration = DateTime.Now - regexMatchStart;
                if (!regexMatch.Success)
                {
                    regexMissCount++;
                    regexMissCumulativeDuration += regexMatchDuration;
                }
                else
                {
                    regexMatchCount++;
                    regexMatchCumulativeDuration += regexMatchDuration;
                }
                return regexMatch;
            }
            return null;
        }
        
        public string Statistics ()
        {
            StringWriter sr = new StringWriter();
            
            int maxWidth = (regexMatchCount + regexMissCount).ToString().Length;
            
            string statsOutputPattern = "  {0,-7} {1," + maxWidth + "} Duration {2} (Avg: {3})";

            if (!string.IsNullOrEmpty(title)) sr.WriteLine("Title: " + title);
            if (!string.IsNullOrEmpty(description)) sr.WriteLine("Description: " + description);
            if (!string.IsNullOrEmpty(example)) sr.WriteLine("Example: " + example);
            sr.WriteLine("Pattern: " + pattern);
            sr.WriteLine(string.Format(statsOutputPattern, "Hits:", regexMatchCount, regexMatchCumulativeDuration, regexMatchCount == 0?new TimeSpan(0):TimeSpan.FromTicks(regexMatchCumulativeDuration.Ticks/regexMatchCount)));
            sr.WriteLine(string.Format(statsOutputPattern, "Misses:", regexMissCount, regexMissCumulativeDuration, regexMissCount == 0?new TimeSpan(0):TimeSpan.FromTicks(regexMissCumulativeDuration.Ticks/regexMissCount)));
            sr.WriteLine(string.Format(statsOutputPattern, "Total:", regexMatchCount + regexMissCount, regexMatchCumulativeDuration + regexMissCumulativeDuration, regexMatchCount + regexMissCount == 0?new TimeSpan(0):TimeSpan.FromTicks((regexMatchCumulativeDuration + regexMissCumulativeDuration).Ticks/(regexMatchCount + regexMissCount))));

            if (shortRegex != null)
            {
                int shortMaxWidth = (shortRegexMatchCount + shortRegexMissCount).ToString().Length;
                
                string shortStatsOutputPattern = "    {0,-7} {1," + shortMaxWidth + "} Duration {2} (Avg: {3})";
                
                sr.WriteLine("  Short Pattern: " + shortPattern);
                sr.WriteLine(string.Format(shortStatsOutputPattern, "Hits:", shortRegexMatchCount, shortRegexMatchCumulativeDuration, shortRegexMatchCount == 0?new TimeSpan(0):TimeSpan.FromTicks(shortRegexMatchCumulativeDuration.Ticks/shortRegexMatchCount)));
                sr.WriteLine(string.Format(shortStatsOutputPattern, "Misses:", shortRegexMissCount, shortRegexMissCumulativeDuration, shortRegexMissCount == 0?new TimeSpan(0):TimeSpan.FromTicks(shortRegexMissCumulativeDuration.Ticks/shortRegexMissCount)));
                sr.WriteLine(string.Format(shortStatsOutputPattern, "Total:", shortRegexMatchCount + shortRegexMissCount, shortRegexMatchCumulativeDuration + shortRegexMissCumulativeDuration, shortRegexMatchCount + shortRegexMissCount == 0?new TimeSpan(0):TimeSpan.FromTicks((shortRegexMatchCumulativeDuration + shortRegexMissCumulativeDuration).Ticks/(shortRegexMatchCount + shortRegexMissCount))));
            }
            
            return sr.ToString();
        }
        
        // If present, these format strings will be used with the groups result from the regex match
        // to generate custom output.
        // stdout and stderr will output immediately.
        // report will be output in order at the end of processing.
        
        // Groups are 1 based.  Should investigate defining 0 as full original line.
        
        // May need to provide option of order of output.  (pass, stdout, stderr is the current default)
        [JsonProperty]
        public string stdoutFormat;
        [JsonProperty]
        public string stderrFormat;
        [JsonProperty]
        public string reportFormat;

        public bool IsValid ()
        {
            // Could test pattern groups against format specifiers here.
            return (pattern != null);
        }
    }

    public class StringFunctionFormatter : IFormatProvider, ICustomFormatter
    {
        public Dictionary<string, OrderedDictionary> replacements = new Dictionary<string, OrderedDictionary>();
  
        // Matchs function_name(parameters)
        public Regex findFunctionsRegex = new Regex(@"([^\s]+)[ ]*\(([^\s]+)\)");

        public delegate string FormatFunction (string inputString, string[] functionAndArgs);
        private Dictionary<string, FormatFunction> formatFunctions = new Dictionary<string, FormatFunction>();
        
        public StringFunctionFormatter ()
        {
            formatFunctions.Add("replace", FormatReplace);
        }
        
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter))
                return this;
            else
                return null;
        }
        
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {   
            if (! formatProvider.Equals(this)) return null;
            
            var workString = arg.ToString();
            
            var functionsToCall = ParseFormatFunctions(format);
            
            foreach (string[] functionAndArgs in functionsToCall)
            {
                if (formatFunctions.ContainsKey(functionAndArgs[0]))
                {
                    workString = formatFunctions[functionAndArgs[0]](workString, functionAndArgs);
                }
                else
                {
                    Console.WriteLine("ERROR - specified format function does not exist: " + functionAndArgs[0]);
                    Environment.Exit(1);
                }
            }
            
            return workString;
        }
        
        public List<string[]> ParseFormatFunctions (string functionString)
        {
            List<string[]> returnList = new List<string[]>();

            if (string.IsNullOrEmpty(functionString)) return returnList;

            Match match = findFunctionsRegex.Match (functionString);

            while (match.Success)
            {
                List<string> tempFunctionAndArgs = new List<string>();
                
                // First group is the full matched string.
                // Second group is the function name.
                tempFunctionAndArgs.Add(match.Groups[1].ToString());
                
                // For more than one arg, they will be separated by commas.
                tempFunctionAndArgs.AddRange(match.Groups[2].ToString().Split(','));

                returnList.Add(tempFunctionAndArgs.ToArray());
                match = match.NextMatch();
            }
            
            return returnList;
        }

        public string FormatReplace(string inputString, string[] functionAndArgs)
        {
            string workString = inputString;
                        
            for (int i = 1; i < functionAndArgs.Length; i++)
            {
                if (replacements.ContainsKey(functionAndArgs[i]))
                {
                    foreach (DictionaryEntry de in replacements[functionAndArgs[i]])
                    {
                        workString = workString.Replace((string)de.Key, (string)de.Value);
                    }
                }
                else
                {
                    Console.WriteLine("ERROR - specified format replace group does not exist: " + functionAndArgs[i]);
                    Environment.Exit(1);
                }
            }
            return workString;
        }
    }
}

