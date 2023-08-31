using System;

namespace TableCloth.Models.Catalog
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EnumDisplayNameAttribute : Attribute
    {
        public EnumDisplayNameAttribute(string displayName)
        {
            this._displayName = displayName;
        }

        private readonly string _displayName;

        public string DisplayName => _displayName;

        public override string ToString() => $"{{ Display Name: {_displayName} }}";
    }
}
