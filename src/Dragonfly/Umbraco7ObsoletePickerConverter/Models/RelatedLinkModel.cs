using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dragonfly.Umbraco7ObsoletePickerConverter.Models
{
    using System.CodeDom;
    using Dragonfly.Umbraco7Helpers;
    using Newtonsoft.Json;
    using Umbraco.Core;
    using Umbraco.Web;

    //{
    //    "Id": null,
    //    "Content": null,
    //    "caption": "Gift Acceptance Policy",
    //    "link": "umb://document/e0231812de4044139bcb00ef99a2e280",
    //    "newWindow": false,
    //    "isInternal": true,
    //    "type": 0,
    //    "edit": true,
    //    "internal": "umb://document/e0231812de4044139bcb00ef99a2e280",
    //    "internalName": "Gift Acceptance Policy",
    //    "internalIcon": "icon-document"
    //}
    internal class RelatedLinkModel
    {
        public int? Id { get; set; }
        public string caption { get; set; }
        public string link { get; set; }
        public bool newWindow { get; set; }
        public bool isInternal { get; set; }
        public string type { get; set; }
        public bool edit { get; set; }

        [JsonProperty("internal")]
        public string Internal { get; set; }
        public string internalName { get; set; }

        public string internalIcon { get; set; }

        public RelatedLinkModel()
        {

        }

        public RelatedLinkModel(Umbraco.Web.Models.RelatedLink UmbracoRelatedLink)
        {
            if (UmbracoRelatedLink.Id != null)
            {
                this.Id = UmbracoRelatedLink.Id;
            }
            else
            {
                var intId = 0;
                var isInt = Int32.TryParse(UmbracoRelatedLink.Link, out intId);
                if (isInt)
                {
                    this.Id = intId;
                }
            }

            if (UmbracoRelatedLink.Content != null)
            {
                var udi = new GuidUdi("document", UmbracoRelatedLink.Content.GetKey());
                this.Internal = udi.ToString();
                this.internalName = UmbracoRelatedLink.Content.Name;
            }
            else
            {
                if (UmbracoRelatedLink.IsInternal)
                {
                    this.Internal = UmbracoRelatedLink.Link ;
                    var umbHelper = new UmbracoHelper(UmbracoContext.Current);
                    var udi = Udi.Parse(this.Internal);
                    var node = umbHelper.TypedContent(udi);
                    this.internalName = node.Name;
                    this.internalIcon = "icon-document";
                }
            }

            this.caption = UmbracoRelatedLink.Caption;
            this.link = UmbracoRelatedLink.Link;
            this.newWindow = UmbracoRelatedLink.NewWindow;
            this.isInternal = UmbracoRelatedLink.IsInternal;
            this.type = UmbracoRelatedLink.Type.ToString();
            this.edit = true;
           
        }
    }
}
