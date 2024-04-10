using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Quote_To_Deal.Common
{
    public class SettingsHelpers
    {
        public static void AddOrUpdateAppSetting<T>(string directory, string sectionPathKey, T value)
        {
            try
            {

                var filePath = Path.Combine(directory, "cache.json");
                string json = File.ReadAllText(filePath);
                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                SetValueRecursively(sectionPathKey, jsonObj, value);

                string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, output);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing app settings | {0}", ex.Message);
            }
        }
        public static T ReadCacheSetting<T>(string directory, string sectionPathKey)
        {
            try
            {
                var filePath = Path.Combine(directory, "cache.json");
                string json = File.ReadAllText(filePath);
                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                return ReadValueRecursively<T>(sectionPathKey, jsonObj);

            }
            catch (Exception ex)
            {
                return default(T);
            }
        }

        public static void SetValueRecursively<T>(string sectionPathKey, dynamic jsonObj, T value)
        {
            // split the string at the first ':' character
            var remainingSections = sectionPathKey.Split(":", 2);

            var currentSection = remainingSections[0];
            if (remainingSections.Length > 1)
            {
                // continue with the procress, moving down the tree
                var nextSection = remainingSections[1];
                SetValueRecursively(nextSection, jsonObj[currentSection], value);
            }
            else
            {
                // we've got to the end of the tree, set the value
                jsonObj[currentSection] = value;
            }
        }

        public static T ReadValueRecursively<T>(string sectionPathKey, dynamic jsonObj)
        {
            return jsonObj[sectionPathKey];
        }
    }
}
