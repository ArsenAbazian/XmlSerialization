This library contains helper for make xml serialization and deserialization process easier...

The SerializationHelper class uses the standard XmlSerializer to save and restore object with some benefits. 

1. The XmlSerializer class creates NEW object from xml file and SerializerHelper allows to RESTORE object's state from xml file, instead of creating a new one.

2. For the XmlSerializer class you should specify all the classes which are allowed to deserialize. Imagine you have object that has collection of items of A class. This collection can hold items of different classes, which are descendant of class A. For XmlSerialize you have to specify all the descendant classes (or you will get the exception when deserialize) using XmlInclude attribute, or collect all the types and pass them in the XmlSerialzer constructor. The SerialzerHelper class do it for you. All you need is to specify base classes in XmlInclude attribute and SerialzerHelper will search in loaded assemblies for all descendant classes and pass them in XmlSerialzer constructor. Please add AllowDynamicTypes attribute to allow the SerialzerHelper class collect all the needed types in assemblies

Example: 

    [Serializable]
    [AllowDynamicTypes]
    [XmlInclude(typeof(WfConnectionPoint))]
    [XmlInclude(typeof(WfConnector))]
    [XmlInclude(typeof(WfNode))]
    public class WfDocument : ISupportSerialization {
        public WfDocument() {
            Nodes = new List<WfNode>(this);
            Connectors = new List<WfConnector>();
        }

        string ISupportSerialization.FileName { get; set; }

        public void Save() {
            if(string.IsNullOrEmpty(FullPath))
                return;
            Save(FullPath);
        }

        [XmlIgnore, Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public string FullPath { get; set; }

        public bool Load(string fileName) {
            FullPath = fileName;
            if(SerializationHelper.Current.Load(this, GetType(), fileName)) {
                FileName = Path.GetFileName(fileName);
                return true;
            }
            return false;
        }

        void ISupportSerialization.OnBeginSerialize() { }
        void ISupportSerialization.OnEndSerialize() { }
        void ISupportSerialization.OnBeginDeserialize() { }
        void ISupportSerialization.OnEndDeserialize() { }

        [Browsable(false)]
        public List<WfNode> Nodes { get; private set; }

        [Browsable(false)]
        public List<WfConnector> Connectors { get; private set; }
    }

    public class WfAbortNode : WfNode { }
    public class WfSwitchNode : WfNode { }
    public class WfProperStoreNode : WfNode { }
