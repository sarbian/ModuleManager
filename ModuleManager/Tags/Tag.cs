using System;

namespace ModuleManager.Tags
{
    public struct Tag
    {
        public readonly string key;
        public readonly string value;
        public readonly string trailer;

        public Tag(string key, string value, string trailer)
        {
            this.key = key ?? throw new ArgumentNullException(nameof(key));
            if (key == string.Empty) throw new ArgumentException("can't be empty", nameof(key));

            if (value == null && trailer != null)
                throw new ArgumentException("trailer must be null if value is null");

            if (trailer == string.Empty) throw new ArgumentException("can't be empty (null allowed)", nameof(trailer));

            this.value = value;
            this.trailer = trailer;
        }

        public override string ToString()
        {
            string s = "< '" + key + "' ";
            if (value != null) s += "[ '" + value + "' ] ";
            if (trailer != null) s += "'" + trailer + "' ";
            s += ">";
            return s;
        }
    }
}
