using System.Text.RegularExpressions;

namespace ACDecompileParser.Tests.Lib.Parser;

public class RegexTest
{
    [Fact]
    public void TestRegex()
    {
        string[] testStrings = {
            "/* 123 */",
            "/*123*/", 
            "/* 1234 */",
            "/*1234*/",
            "/* 1 */",
            "/*1*/"
        };
        
        string regexPattern = @"\/\*\s*(\d+)\s*\*\/";
        
        foreach (var test in testStrings)
        {
            var match = Regex.IsMatch(test, regexPattern);
            Console.WriteLine($"'{test}' matches pattern '{regexPattern}': {match}");
            if (match)
            {
                var groups = Regex.Match(test, regexPattern);
                Console.WriteLine($"  Group 1 (digits): '{groups.Groups[1].Value}'");
            }
        }
    }
}
