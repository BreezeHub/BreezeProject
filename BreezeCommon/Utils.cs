using System;
using System.Collections.Generic;
using System.Text;

namespace BreezeCommon
{
    public class Utils
    {
    }

    public class ConfigurationOptionWrapper<T>
    {
        public string Name { get; set; }
        public T Value { get; set; }

        public ConfigurationOptionWrapper(string name, T configValue)
        {
            this.Name = name;
            this.Value = configValue;
        }
    }
}
