namespace Dragonfly.Umbraco7ObsoletePickerConverter.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Umbraco.Core.Models;

    internal class DocTypeWithProperty
    {
        public string DocTypeAlias { get; set; }
        public IContentType ContentType { get; set; }
        public PropertyType Property { get; set; }

        public DocTypeWithProperty()
        {

        }

        public DocTypeWithProperty(IContentType ContentType, PropertyType PropertyVal)
        {
            this.ContentType = ContentType;
            this.DocTypeAlias = ContentType.Alias;
            this.Property = PropertyVal;
        }
    }
}
