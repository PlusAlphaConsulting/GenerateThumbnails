using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace GenerateThumbnails.Helpers
{
    internal class HelpersBasic
    {
        public static IActionResult ReturnErrorException(ILogger log, Exception ex, string prefixMessage = null)
        {
            var message = "";
            
            return ReturnErrorException(log,
                (prefixMessage == null ? string.Empty : prefixMessage + " : ") + ex.Message + message);
        }

        public static IActionResult ReturnErrorException(ILogger log, string message, string region = null)
        {
            LogError(log, message, region);
            return new BadRequestObjectResult(
                new JObject
                {
                    {"success", false},
                    {"errorMessage", message},
                    {
                        "operationsVersion",
                        AssemblyName.GetAssemblyName(Assembly.GetExecutingAssembly().Location).Version.ToString()
                    }
                }.ToString());
        }

        public static void LogError(ILogger log, string message, string azureRegion = null)
        {
            log.LogError((azureRegion != null ? "[" + azureRegion + "] " : "") + message);
        }

        /// <summary>
        /// Generates a unique name based on a prefix. Useful for creating unique names for assets, locators, etc.
        /// </summary>
        /// <param name="prefix">Prefix of the name</param>
        /// <param name="length">Lenght of the unique name (without the '-' before)</param>
        /// <returns></returns>
        public static string GenerateUniqueName(string prefix, int length = 8)
        {
            // return a string of length "length" containing random characters

            return prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, length);
        }
    }
}