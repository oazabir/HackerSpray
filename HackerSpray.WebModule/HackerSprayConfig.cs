using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackerSpray.WebModule
{
    public class HackerSprayConfig : ConfigurationSection
    {
        private static HackerSprayConfig settings = ConfigurationManager.GetSection("HackerSprayConfig") as HackerSprayConfig;

        public static HackerSprayConfig Settings
        {
            get
            {
                return settings;
            }
        }


        [ConfigurationProperty("redis", IsRequired = true)]
        //[StringValidator(MinLength = 1, MaxLength = 1024)]
        public string Redis
        {
            get { return (string)this["redis"]; }
            set { this["redis"] = value; }
        }

        [ConfigurationProperty("prefix", IsRequired = true)]
        //[StringValidator(MinLength = 1, MaxLength = 1024)]
        public string Prefix
        {
            get { return (string)this["prefix"]; }
            set { this["prefix"] = value; }
        }

        [ConfigurationProperty("keys", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(KeyElement))]
        public GenericConfigurationElementCollection<KeyElement> Paths
        {
            get { return (GenericConfigurationElementCollection<KeyElement>)this["keys"]; }
            set { this["keys"] = value; }
        }
    }

    public class GenericConfigurationElementCollection<T> : ConfigurationElementCollection, IEnumerable<T> where T : ConfigurationElement, new()
    {
        List<T> _elements = new List<T>();

        protected override ConfigurationElement CreateNewElement()
        {
            T newElement = new T();
            _elements.Add(newElement);
            return newElement;
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return _elements.Find(e => e.Equals(element));
        }

        public new IEnumerator<T> GetEnumerator()
        {
            return _elements.GetEnumerator();
        }
    }
    

    public class KeyElement : ConfigurationElement
    {
        //Make sure to set IsKey=true for property exposed as the GetElementKey above
        [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
        public string Name
        {
            get { return (string)base["name"]; }
            set { base["name"] = value; }
        }

        [ConfigurationProperty("post", IsRequired = true)]
        public bool Post
        {
            get { return (bool)base["post"]; }
            set { base["post"] = value; }
        }

        [ConfigurationProperty("maxAttempts", IsRequired = true)]
        public long MaxAttempts
        {
            get { return (long)base["maxAttempts"]; }
            set { base["maxAttempts"] = value; }
        }

        [ConfigurationProperty("interval", IsRequired = true)]
        public TimeSpan Interval
        {
            get { return (TimeSpan)base["interval"]; }
            set { base["interval"] = value; }
        }

        [ConfigurationProperty("mode", IsRequired = true)]
        public string Mode
        {
            get { return (string)base["mode"]; }
            set { base["mode"] = value; }
        }
    }
}
