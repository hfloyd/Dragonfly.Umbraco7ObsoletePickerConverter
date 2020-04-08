namespace Dragonfly
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Web;
    using System.Web.Mvc;
    using Dragonfly.NetModels;
    using Dragonfly.Umbraco7Helpers;
    using Dragonfly.Umbraco7ObsoletePickerConverter;
    using Newtonsoft.Json;
    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Services;
    using Umbraco.Web;
    using Umbraco.Web.WebApi;

    // /Umbraco/Api/Umbraco7ObsoletePickerConverterApi <-- UmbracoApiController

    // [IsBackOffice]
    // /Umbraco/backoffice/Api/Umbraco7ObsoletePickerConverterApi <-- UmbracoAuthorizedApiController

    [IsBackOffice]
    public class Umbraco7ObsoletePickerConverterApiController : UmbracoAuthorizedApiController
    {
        #region Converting Old Picker Ints to New Udis

        /// /Umbraco/backoffice/Api/Umbraco7ObsoletePickerConverterApi/CheckAllDataTypes?DoUpdates=false
        [System.Web.Http.AcceptVerbs("GET")]
        public HttpResponseMessage CheckAllDataTypes(bool DoUpdates = false)
        {
            var fixerService = new PickerFixService(Services);
            IDataTypeService dataTypeService = Services.DataTypeService;

            var returnSB = new StringBuilder();
            var mainStatus = new StatusMessage();

            var allDts = dataTypeService.GetAllDataTypeDefinitions();

            var dtsOK = new List<IDataTypeDefinition>();
            var dtsToCheckData = new List<IDataTypeDefinition>();
            var dtsToChangePropEd = new List<IDataTypeDefinition>();

            foreach (var dt in allDts)
            {
                if (fixerService.PropertyEditorTypesNewToCheck().Contains(dt.PropertyEditorAlias))
                {
                    dtsToCheckData.Add(dt);
                    if (DoUpdates)
                    {
                        var updateStatus = fixerService.DoUpdatePickerData(dt.Id);
                        mainStatus.InnerStatuses.Add(updateStatus);
                    }
                }
                else if (fixerService.PropertyEditorTypesOldToChange().Contains(dt.PropertyEditorAlias))
                {
                    dtsToChangePropEd.Add(dt);
                }
                else
                {
                    dtsOK.Add(dt);
                }
            }

            //Update Message Details
            mainStatus.MessageDetails += "All the DataTypes have been checked. These are the results:\n";
            mainStatus.MessageDetails += $"== DataTypes which need to have their PropertyEditor changed to the new version: {dtsToChangePropEd.Count}\n";
            foreach (var def in dtsToChangePropEd)
            {
                mainStatus.MessageDetails += $"-- {def.Name} ({def.PropertyEditorAlias}) #{def.Id} \n";
            }
            mainStatus.MessageDetails += $"== DataTypes which need to have their data checked and converted: {dtsToCheckData.Count}\n";
            foreach (var def in dtsToCheckData)
            {
                mainStatus.MessageDetails += $"-- {def.Name} ({def.PropertyEditorAlias}) #{def.Id} \n";
            }
            mainStatus.MessageDetails += $"== DataTypes which do NOT need to be checked or updated: {dtsOK.Count}\n";
            foreach (var def in dtsOK)
            {
                mainStatus.MessageDetails += $"-- {def.Name} ({def.PropertyEditorAlias}) #{def.Id} \n";
            }

            mainStatus.Success = true;
            mainStatus.Message = $"UpdateAllPickersData Completed without Errors'";


            string json = JsonConvert.SerializeObject(mainStatus);

            returnSB.AppendLine(json);

            return new HttpResponseMessage()
            {
                Content = new StringContent(
                    returnSB.ToString(),
                    Encoding.UTF8,
                    "application/json"
                )
            };
        }

        /// /Umbraco/backoffice/Api/Umbraco7ObsoletePickerConverterApi/UpdateDataTypePickerData?DataTypeGuid=xxx
        [System.Web.Http.AcceptVerbs("GET")]
        public HttpResponseMessage UpdateDataTypePickerData(string DataTypeGuid)
        {
            var fixerService = new PickerFixService(Services);
            var returnSB = new StringBuilder();

            var dtGuid = new Guid(DataTypeGuid);
            var mainStatus = fixerService.DoUpdatePickerData(dtGuid);

            string json = JsonConvert.SerializeObject(mainStatus);

            returnSB.AppendLine(json);

            return new HttpResponseMessage()
            {
                Content = new StringContent(
                    returnSB.ToString(),
                    Encoding.UTF8,
                    "application/json"
                )
            };
        }

        /// /Umbraco/backoffice/Api/Umbraco7ObsoletePickerConverterApi/UpdateDataTypePickerData?DataTypeId=0
        [System.Web.Http.AcceptVerbs("GET")]
        public HttpResponseMessage UpdateDataTypePickerData(int DataTypeId)
        {
            var fixerService = new PickerFixService(Services);
            var returnSB = new StringBuilder();
            
            var mainStatus = fixerService.DoUpdatePickerData(DataTypeId);

            string json = JsonConvert.SerializeObject(mainStatus);

            returnSB.AppendLine(json);

            return new HttpResponseMessage()
            {
                Content = new StringContent(
                    returnSB.ToString(),
                    Encoding.UTF8,
                    "application/json"
                )
            };
        }
   
        #endregion

        #region Tests & Examples

        /// /Umbraco/backoffice/Api/AuthorizedApi/Test
        [System.Web.Http.AcceptVerbs("GET")]
        public bool Test()
        {
            //LogHelper.Info<AuthorizedApiController>("Test STARTED/ENDED");
            return true;
        }

        /// /Umbraco/backoffice/Api/AuthorizedApi/ExampleReturnHtml
        [System.Web.Http.AcceptVerbs("GET")]
        public HttpResponseMessage ExampleReturnHtml()
        {
            var returnSB = new StringBuilder();

            returnSB.AppendLine("<h1>Hello! This is HTML</h1>");
            returnSB.AppendLine(
                "<p>Use this type of return when you want to exclude &lt;XML&gt;&lt;/XML&gt; tags from your output and don\'t want it to be encoded automatically.</p>");

            return new HttpResponseMessage()
            {
                Content = new StringContent(
                    returnSB.ToString(),
                    Encoding.UTF8,
                    "text/html"
                )
            };
        }

        /// /Umbraco/backoffice/Api/AuthorizedApi/ViewRenderedHtml
        [System.Web.Http.AcceptVerbs("GET")]
        public HttpResponseMessage ViewRenderedHtml()
        {
            var returnSB = new StringBuilder();

            var pvPath = "~/Views/Partials/xxx/xxx.cshtml";

            //GET DATA TO DISPLAY
            var myDataModel = new StatusMessage(true,
                "Some sort of object that can be passed as the model to the View specified...");

            //VIEW DATA 
            var viewData = new ViewDataDictionary();
            viewData.Model = myDataModel;
            viewData.Add("DisplayTitle", $"Add additional variables via the View Data as needed....");
            viewData.Add("AnotherParameter", true);

            //RENDER
            var controllerContext = this.ControllerContext;
            var displayHtml =
                ApiControllerHtmlHelper.GetPartialViewHtml(controllerContext, pvPath, viewData, HttpContext.Current);
            returnSB.AppendLine(displayHtml);

            //RETURN AS HTML
            return new HttpResponseMessage()
            {
                Content = new StringContent(
                    returnSB.ToString(),
                    Encoding.UTF8,
                    "text/html"
                )
            };
        }

        /// /Umbraco/backoffice/Api/AuthorizedApi/ExampleReturnJson
        [System.Web.Http.AcceptVerbs("GET")]
        public HttpResponseMessage ExampleReturnJson()
        {
            var returnSB = new StringBuilder();

            var testData = new StatusMessage(true, "This is a test object so you can see JSON!");
            string json = JsonConvert.SerializeObject(testData);

            returnSB.AppendLine(json);

            return new HttpResponseMessage()
            {
                Content = new StringContent(
                    returnSB.ToString(),
                    Encoding.UTF8,
                    "application/json"
                )
            };
        }

        /// /Umbraco/backoffice/Api/SiteAuditorApi/ExampleReturnCsv
        [System.Web.Http.AcceptVerbs("GET")]
        public HttpResponseMessage ExampleReturnCsv()
        {
            var returnSB = new StringBuilder();
            var tableData = new StringBuilder();

            for (int i = 0; i < 10; i++)
            {
                tableData.AppendFormat(
                    "\"{0}\",{1},\"{2}\",{3}{4}",
                    "Name " + i,
                    i,
                    string.Format("Some text about item #{0} for demo.", i),
                    DateTime.Now,
                    Environment.NewLine);
            }

            returnSB.Append(tableData);

            return StringBuilderToFile(returnSB, "Example.csv");
        }

        #region Shared Functions

        private static HttpResponseMessage StringBuilderToFile(StringBuilder StringData,
            string OutputFileName = "Export.csv", string MediaType = "text/csv")
        {
            //TODO: Need to figure out why » is returning as Â (likely an issue with unicode in general...?)

            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(StringData.ToString());
            writer.Flush();
            stream.Position = 0;

            HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);
            result.Content = new StreamContent(stream);
            result.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaType);
            result.Content.Headers.ContentDisposition =
                new ContentDispositionHeaderValue("attachment") { FileName = OutputFileName };
            return result;
        }

        #endregion

        #endregion

    }
}
