using System;
using System.Configuration;
using System.Linq;

namespace SimpleJsonRest {
    public class Config : ConfigurationSection {
        [ConfigurationProperty("name", IsRequired = false)]
        public string Name {
            get { return (string) this["name"]; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("service", IsRequired = true)]
        public string Service {
            get { return (string)this["service"]; }
            set { this["service"] = value; }
        }

        [ConfigurationProperty("logPath", IsRequired = true)]
        public string LogPath {
            get { return (string)this["logPath"]; }
            set { this["logPath"] = value; }
        }

        Type serviceType;
        public Type ServiceType {
            get {
                if (serviceType == null) {
                    serviceType = AppDomain.CurrentDomain.GetAssemblies()
                                    .SelectMany(a => a.GetTypes())
                                    .Where(t => t.FullName == Service && typeof(IAuthentifiedService).IsAssignableFrom(t) && !t.IsInterface)
                                    .FirstOrDefault();
                    if (serviceType == null) throw new HandlerException($"Type {Service} not found");
                }

                return serviceType;
            }
        }
    }
}