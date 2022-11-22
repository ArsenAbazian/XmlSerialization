using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace XmlSerialization {
    public class SerializationHelper : ISerializationHelper {
        static ISerializationHelper current = new SerializationHelper();
        public static ISerializationHelper Current {
            get {
                if(current == null)
                    current = new SerializationHelper();
                return current;
            }
            set { current = value; }
        }

        public virtual void AssignValueProperties(object src, object dst) {
            PropertyInfo[] props = src.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach(var prop in props) {
                if(!prop.CanRead)
                    continue;
                if(prop.PropertyType.IsValueType || prop.PropertyType.IsEnum || prop.PropertyType == typeof(string)) {
                    if(prop.CanWrite && prop.GetAccessors().Length == 2)
                        prop.SetValue(dst, prop.GetValue(src, null));
                }
            }
        }

        protected bool LoadCore(ISupportSerialization obj, ISupportSerialization loaded, Type t) {
            obj.OnBeginDeserialize();
            PropertyInfo[] props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach(var prop in props) {
                if(!prop.CanRead)
                    continue;
                if(prop.GetCustomAttribute(typeof(XmlIgnoreAttribute)) != null)
                    continue;
                if(prop.CanWrite && prop.GetAccessors().Length == 2) {
                    prop.SetValue(obj, prop.GetValue(loaded, null));
                }
                else {
                    var value = prop.GetValue(loaded, null);
                    if(value is IList) {
                        IList srcList = (IList)value;
                        var dl = prop.GetValue(obj, null);
                        if(dl is IList) {
                            IList dstList = (IList)dl;
                            dstList.Clear();
                            for(int i = 0; i < srcList.Count; i++) {
                                dstList.Add(srcList[i]);
                            }
                        }
                    }
                    else if(value is IDictionary) {
                        IDictionary srcDict = (IDictionary)value;
                        var dict = prop.GetValue(obj, null);
                        if(dict is IDictionary) {
                            IDictionary dstDict = (IDictionary)dict;
                            dstDict.Clear();
                            foreach(object key in srcDict.Keys) {
                                dstDict.Add(key, srcDict[key]);
                            }
                        }
                    }
                }
            }
            obj.OnEndDeserialize();
            return true;
        }

        public bool Save(ISupportSerialization obj, Type t, StringBuilder b) {
            if(b == null)
                return false;
            try {
                XmlSerializer formatter = new XmlSerializer(t, GetExtraTypes(t));
                obj.OnBeginSerialize();
                TextWriter writer = new StringWriter(b);
                formatter.Serialize(writer, obj);
                obj.OnEndSerialize();
            }
            catch(Exception) {
                return false;
            }

            return true;
        }

        public bool LoadFromString(ISupportSerialization obj, Type t, string text) {
            if(text == null)
                return true;
            var loaded = FromString(text, t);
            if(loaded == null)
                return false;
            return LoadCore(obj, loaded, t);
        }

        public bool Load(ISupportSerialization obj, Type t, string fileName) {
            var loaded = FromFile(fileName, t);
            if(loaded == null)
                return false;
            return LoadCore(obj, loaded, t);
        }
        public ISupportSerialization FromFile(string fileName, Type t) {
            if(string.IsNullOrEmpty(fileName))
                return null;
            if(!File.Exists(fileName))
                return null;
            var extra = GetExtraTypes(t);
            XmlSerializer formatter = new XmlSerializer(t, extra);
            try {
                ISupportSerialization obj = null;
                using(FileStream fs = new FileStream(fileName, FileMode.OpenOrCreate)) {
                    obj = (ISupportSerialization)formatter.Deserialize(fs);
                }
                obj.FileName = fileName;
                obj.OnEndDeserialize();
                return obj;
            }
            catch(Exception) {
                return null;
            }
        }

        public ISupportSerialization FromString(string text, Type t) {
            if(string.IsNullOrEmpty(text))
                return null;
            var extra = GetExtraTypes(t);
            XmlSerializer formatter = new XmlSerializer(t, extra);
            try {
                ISupportSerialization obj = null;
                TextReader r = new StringReader(text);
                obj = (ISupportSerialization)formatter.Deserialize(r);
                obj.OnEndDeserialize();
                return obj;
            }
            catch(Exception) {
                return null;
            }
        }

        protected virtual void GetExtraTypes(List<Type> extra, Type t) {
            var baseTypes = t.GetCustomAttributes<XmlIncludeAttribute>().ToList();
            var asms = Assembly.GetEntryAssembly().GetReferencedAssemblies().ToList();
            asms.Add(Assembly.GetEntryAssembly().GetName());
            Dictionary<string, Assembly> processedAssemblies = new Dictionary<string, Assembly>();
            foreach(var aname in asms) {
                try {
                    Assembly assembly = Assembly.Load(aname);
                    var name = assembly.GetName();
                    if(name != null) {
                        var key = name.Name;
                        if(key != null && processedAssemblies.ContainsKey(key))
                            continue;
                        processedAssemblies.Add(key, assembly);
                        AddExtraTypes(extra, baseTypes, assembly);
                    }
                }
                catch(Exception) { }
            }
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if(processedAssemblies.ContainsKey(assembly.GetName().Name))
                    continue;
                processedAssemblies.Add(assembly.GetName().Name, assembly);
                try {
                    AddExtraTypes(extra, baseTypes, assembly);
                }
                catch(Exception) { }
            }
        }

        public virtual Type[] GetExtraTypes(Type t) {
            var allow = t.GetCustomAttribute<AllowDynamicTypesAttribute>();
            if(allow == null || !allow.Allow)
                return new Type[0];
            List<Type> extra = new List<Type>();
            GetExtraTypes(extra, t);
            
            return extra.ToArray();
        }

        protected virtual void AddExtraTypes(List<Type> extra, List<XmlIncludeAttribute> baseTypes, Assembly assembly) {
            foreach(Type tp in assembly.GetTypes()) {
                if(!tp.IsClass && tp.IsAbstract)
                    continue;
                foreach(var a in baseTypes) {
                    if(a != null && a.Type.IsAssignableFrom(tp)) {
                        if(!extra.Contains(tp)) {
                            extra.Add(tp);
                            GetExtraTypes(extra, tp);
                        }
                    }
                }
            }
        }

        public bool Save(ISupportSerialization obj, Type t, string path) {
            if(!string.IsNullOrEmpty(Path.GetExtension(path))) {
                obj.FileName = Path.GetFileName(obj.FileName);
                path = Path.GetDirectoryName(path);
            }
            if(File.Exists(obj.FileName))
                obj.FileName = Path.GetFileName(obj.FileName);
            string fullName = string.IsNullOrEmpty(path) ? obj.FileName : path + "\\" + obj.FileName;
            string tmpFile = Path.GetFileNameWithoutExtension(fullName) + ".tmp";
            if(!string.IsNullOrEmpty(path))
                tmpFile = path + "\\" + tmpFile;

            if(string.IsNullOrEmpty(obj.FileName))
                return false;
            try {
                XmlSerializer formatter = new XmlSerializer(t, GetExtraTypes(t));
                using(FileStream fs = new FileStream(tmpFile, FileMode.Create)) {
                    obj.OnBeginSerialize();
                    formatter.Serialize(fs, obj);
                    obj.OnEndSerialize();
                }
                if(File.Exists(fullName))
                    File.Delete(fullName);
                File.Move(tmpFile, fullName);
            }
            catch(Exception) {
                return false;
            }

            return true;
        }
    }

    public interface ISerializationHelper {
        bool Load(ISupportSerialization obj, Type type, string fileName);
        bool LoadFromString(ISupportSerialization obj, Type type, string text);
        bool Save(ISupportSerialization obj, Type type, string fullName);
        bool Save(ISupportSerialization obj, Type type, StringBuilder b);
        ISupportSerialization FromFile(string fileName, Type type);
        Type[] GetExtraTypes(Type t);
    }

    public interface ISupportSerialization {
        string FileName { get; set; }
        void OnBeginDeserialize();
        void OnEndDeserialize();
        void OnBeginSerialize();
        void OnEndSerialize();
    }

    public class AllowDynamicTypesAttribute : Attribute {
        public AllowDynamicTypesAttribute() : this(true) { }
        public AllowDynamicTypesAttribute(bool allow) {
            Allow = allow;
        }
        public bool Allow { get; private set; }
    }
}
