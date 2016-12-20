﻿namespace Unosquare.Swan.Abstractions
{
    using Formatters;
    using Reflection;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Represents a provider to save and load settings using a plain JSON file
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SettingsProvider<T> : SingletonBase<SettingsProvider<T>>
    {
        private T m_Global;

        /// <summary>
        /// Gets or sets the configuration file path. By default the entry assembly directory is used
        /// and the filename is appsettings.json.
        /// </summary>
        public virtual string ConfigurationFilePath { get; set; } = Path.Combine(CurrentApp.EntryAssemblyDirectory, "appsettings.json");

        /// <summary>
        /// Gets the global settings object
        /// </summary>
        public T Global
        {
            get
            {
                lock (SyncRoot)
                {
                    if (m_Global == null)
                        ReloadGlobalSettings();

                    return m_Global;
                }
            }
        }

        /// <summary>
        /// Reloads the global settings.
        /// </summary>
        public void ReloadGlobalSettings()
        {
            lock (SyncRoot)
            {
                if (File.Exists(ConfigurationFilePath) == false || File.ReadAllText(ConfigurationFilePath).Length == 0)
                {
                    m_Global = Activator.CreateInstance<T>();
                    PersistGlobalSettings();
                }

                m_Global = JsonFormatter.Deserialize<T>(File.ReadAllText(ConfigurationFilePath));
            }
        }

        /// <summary>
        /// Gets the json data.
        /// </summary>
        /// <returns></returns>
        public string GetJsonData()
        {
            lock (SyncRoot)
            {
                return File.ReadAllText(ConfigurationFilePath);
            }
        }

        /// <summary>
        /// Persists the global settings.
        /// </summary>
        public void PersistGlobalSettings()
        {
            lock (SyncRoot)
            {
                var stringData = JsonFormatter.Serialize(Global);
                File.WriteAllText(ConfigurationFilePath, stringData);
            }
        }

        /// <summary>
        /// Updates settings from list.
        /// </summary>
        /// <param name="list">The list.</param>
        public List<string> RefreshFromList(List<ExtendedPropertyInfo<T>> list)
        {
            List<string> changedSettings = new List<string>();

            foreach (var property in list)
            {
                var prop = Instance.Global.GetType().GetTypeInfo().GetProperty(property.Property);
                var originalValue = prop.GetValue(Instance.Global);
                bool isChanged = false;

                if (prop.PropertyType.IsArray)
                {
                    var itemType = prop.PropertyType.GetElementType();

                    var coll = property.Value as IEnumerable;
                    if (coll == null) continue;

                    var arr = Array.CreateInstance(itemType, coll.Cast<object>().Count());

                    var i = 0;
                    foreach (var value in coll)
                    {
                        object itemvalue;
                        if (Constants.BasicTypesInfo[itemType].TryParse(value.ToString(), out itemvalue))
                            arr.SetValue(itemvalue, i++);
                    }

                    prop.SetValue(Instance.Global, arr);
                }
                else
                {
                    if (property.Value == null)
                    {
                        if (originalValue == null) continue;

                        isChanged = true;
                        prop.SetValue(Instance.Global, null);
                    }
                    else
                    {
                        object propertyValue;
                        if (Constants.BasicTypesInfo[prop.PropertyType].TryParse(property.Value.ToString(), out propertyValue))
                        {
                            if (propertyValue == originalValue) continue;

                            isChanged = true;
                            prop.SetValue(Instance.Global, property.Value);
                        }
                    }
                }

                if (isChanged)
                {
                    changedSettings.Add(property.Property);
                    Instance.PersistGlobalSettings();
                }
            }

            return changedSettings;
        }

        /// <summary>
        /// Gets the list.
        /// </summary>
        /// <returns></returns>
        public List<ExtendedPropertyInfo<T>> GetList()
        {
            var dict = JsonFormatter.Deserialize<Dictionary<string, object>>(GetJsonData());

            return dict.Keys
                    .Select(x => new ExtendedPropertyInfo<T>(x) { Value = dict[x] })
                    .ToList();
        }
    }
}
