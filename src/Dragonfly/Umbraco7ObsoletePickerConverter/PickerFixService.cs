

namespace Dragonfly.Umbraco7ObsoletePickerConverter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Dragonfly.NetModels;
    using Dragonfly.Umbraco7Helpers;
    using Dragonfly.Umbraco7ObsoletePickerConverter.Models;
    using Dragonfly.Umbraco7Services;
    using Newtonsoft.Json;
    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Services;
    using Umbraco.Web;
    using Umbraco.Web.Editors;

    internal class PickerFixService
    {
        IContentService _contentService;
        IDataTypeService _dataTypeService;
        IContentTypeService _contentTypeService;
        IMediaService _mediaService;
        IMemberService _memberService;
        UmbracoHelper _umbracoHelper;

        public IEnumerable<StatusMessage> Messages { get; internal set; }
        public PickerFixService(ServiceContext Services, UmbracoHelper UmbHelper)
        {
            _contentService = Services.ContentService;
            _dataTypeService = Services.DataTypeService;
            _contentTypeService = Services.ContentTypeService;
            _mediaService = Services.MediaService;
            _memberService = Services.MemberService;
            _umbracoHelper = UmbHelper;
        }

        #region Reference Constants
        internal IEnumerable<string> PropertyEditorTypesNewToCheck()
        {
            var list = new List<string>();
            //Content
            list.Add("Umbraco.ContentPicker2");
            list.Add("Umbraco.RelatedLinks2");

            //Media
            list.Add("Umbraco.MediaPicker2");

            //Member
            list.Add("Umbraco.MemberPicker2");

            //Mixed
            list.Add("Umbraco.MultiNodeTreePicker2");

            return list;
        }

        internal IEnumerable<string> PropertyEditorTypesOldToChange()
        {
            var list = new List<string>();
            list.Add("Umbraco.ContentPickerAlias");
            list.Add("Umbraco.MediaPicker");
            list.Add("Umbraco.MultipleMediaPicker");
            list.Add("Umbraco.MultiNodeTreePicker");
            list.Add("Umbraco.Umbraco.MemberPicker");
            list.Add("Umbraco.RelatedLinks");
            list.Add("Umbraco.MemberPicker");
            list.Add("Umbraco.RelatedLinks");

            return list;
        }
        #endregion

        internal StatusMessage DoUpdatePickerData(Guid DataTypeGuid)
        {
            var dt = _dataTypeService.GetDataTypeDefinitionById(DataTypeGuid);

            return DoUpdatePickerData(dt);
        }
        internal StatusMessage DoUpdatePickerData(int DataTypeId)
        {
            var dt = _dataTypeService.GetDataTypeDefinitionById(DataTypeId);

            return DoUpdatePickerData(dt);
        }
        internal StatusMessage DoUpdatePickerData(IDataTypeDefinition DataType)
        {
            var mainStatus = new StatusMessage();
            var summaryStatus = new StatusMessage();
            var dt = DataType;

            var mediaAtRoot = _umbracoHelper.TypedMediaAtRoot();
            var test = mediaAtRoot.Count();
            var mediaFinder = new MediaFinderService(_umbracoHelper, mediaAtRoot);
            var test2 = mediaFinder.HasRootMedia();

            if (PropertyEditorTypesNewToCheck().Contains(dt.PropertyEditorAlias))
            {
                var allDocTypesWithProps = GetAllDocTypesWithProps();
                var docTypesWithDt = allDocTypesWithProps.Where(n => n.Property.DataTypeDefinitionId == dt.Id).ToList();

                if (!docTypesWithDt.Any())
                {
                    mainStatus.Message =
                        $"DataType '{dt.Name}' using editor '{dt.PropertyEditorAlias}' does not have any DocTypes. There is nothing to check or update.";
                    mainStatus.Success = true;
                    return mainStatus;
                }

                var groups = docTypesWithDt.GroupBy(n => n.ContentType);
                foreach (var dtGroup in groups)
                {
                    var docType = dtGroup.Key;
                    var props = new List<DocTypeWithProperty>();

                    foreach (var item in dtGroup)
                    {
                        props.Add(item);
                    }

                    summaryStatus.MessageDetails += $"Doctype '{docType.Alias}':\n";
                    summaryStatus.MessageDetails += $"-Props: {string.Join(", ", props.Select(n => n.Property.Alias))}\n";

                    var nodesToUpdate = _contentService.GetContentOfContentType(docType.Id).ToList();
                    summaryStatus.MessageDetails += $"-Nodes '{nodesToUpdate.Count()}':\n";
                    foreach (var node in nodesToUpdate)
                    {
                        var nodeMsg = $"--Node: # {node.Id} '{node.Name}'";

                        foreach (var prop in props)
                        {
                            var propAlias = prop.Property.Alias;
                            nodeMsg += $"\n---Prop: {propAlias} ";

                            var currData = node.GetValue(propAlias);
                            if (currData == null)
                            {
                                nodeMsg += $" [old data: NONE] - SKIP ";
                            }
                            else
                            {
                                nodeMsg += $" [old data: {currData.ToString()}] ";
                                if (currData.ToString().StartsWith("umb:"))
                                {
                                    //we have a UDI, skip
                                    nodeMsg += $"  ALREADY UDI - SKIP ";
                                }
                                else
                                {
                                    var nodeUpdateStatus = DoUpdateNodePropertyData(node, prop.Property, mediaFinder);
                                    var udi = nodeUpdateStatus.ObjectGuid;
                                    nodeMsg += $"  NEW UDI = {udi} ";
                                    mainStatus.InnerStatuses.Add(nodeUpdateStatus);
                                }
                            }
                        }
                        summaryStatus.MessageDetails += $"{nodeMsg}\n";
                    }

                }

                summaryStatus.Message = "Summary of Node Operations";
                summaryStatus.Success = true;
                mainStatus.InnerStatuses.Add(summaryStatus);

                mainStatus.Message =
                    $"DataType '{dt.Name}' using editor '{dt.PropertyEditorAlias}' will be updated... ";
                mainStatus.Success = true;
            }
            else
            {
                mainStatus.Success = false;
                mainStatus.Message =
                    $"DataType '{dt.Name}' is not one of the types which should have its data updated. It uses PropertyEditor '{dt.PropertyEditorAlias}'";
            }

            return mainStatus;
        }


        internal StatusMessage DoUpdateNodePropertyData(IContent ContentNode, PropertyType Property, MediaFinderService MediaFinder)
        {
            var status = new StatusMessage();

            var propAlias = Property.Alias;
            var currData = ContentNode.GetValue(propAlias);

            if (currData.ToString().StartsWith("umb:"))
            {
                //we already have a UDI
                status.Success = true;
                status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' Data is already a UDI";
                status.ObjectGuid = currData.ToString();
                status.Code = "NoUpdateNeeded";

                return status;
            }

            var type = GetNodeTypeForProperty(Property);

            //Check for special Data formats
            if (Property.PropertyEditorAlias == "Umbraco.MultiNodeTreePicker2")
            {
                var nodeInfo = _umbracoHelper.TypedContent(ContentNode.Id);
                IPublishedContent oldValueIPubSingle = null;
                IEnumerable<IPublishedContent> oldValueIPubMulti = new List<IPublishedContent>();
                var newUdis = new List<string>();

                //check for valid node data
                try
                {
                    oldValueIPubMulti = nodeInfo.GetPropertyValue<IEnumerable<IPublishedContent>>(propAlias);
                    foreach (var iPub in oldValueIPubMulti)
                    {
                        newUdis.Add(iPub.ToUdiString());
                    }
                }
                catch (Exception e)
                {
                    //didn't work, try looking up via Id...
                    var foundUdis = GetUdisFromUnknownIntData(currData.ToString()).ToList();

                    if (foundUdis.Any())
                    {
                        newUdis = foundUdis.Select(n => n.ToString()).ToList();
                    }
                    else
                    {
                        //Maybe it's a Media node name?
                        var matchingMedia = MediaFinder.GetMediaByName(currData.ToString()).ToList();

                        if (matchingMedia.Any())
                        {
                            foreach (var media in matchingMedia)
                            {
                                newUdis.Add(media.ToUdiString("media"));
                            }

                        }
                    }
                }

                if (newUdis.Any())
                {
                    //We've got something to work with!
                    var newData = "";
                    if (newUdis.Count() == 1)
                    {
                        var singleUdi = newUdis.First();
                        newData = singleUdi;
                    }
                    else
                    {
                        newData = JsonConvert.SerializeObject(newUdis);
                    }

                    //status.Success = false;
                    //status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' will be updated";
                    //status.MessageDetails += $"New Data: {newData}\n";
                    //status.Code = "NotImplemented";
                    //return status;

                    //update Content node
                    var valIsValid = Property.IsPropertyValueValid(newData);
                    if (valIsValid)
                    {
                        ContentNode.SetValue(propAlias, newData);
                        var saveStatus = _contentService.SaveAndPublishWithStatus(ContentNode);

                        if (saveStatus.Success)
                        {
                            status.Success = true;
                            status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' Data Updated to '{newData}'";
                            status.ObjectGuid = newData;
                            status.Code = "Updated";
                        }
                        else
                        {
                            status.Success = false;
                            status.Message = $"Unable to update Node #{ContentNode.Id} Property '{propAlias}' to '{newData}'";
                            status.ObjectGuid = newData;
                            status.Code = "SaveError";
                            status.RelatedException = saveStatus.Exception;
                        }
                    }
                    else
                    {
                        status.Success = false;
                        status.Message = $"Unable to update Node #{ContentNode.Id} Property '{propAlias}' to '{newData}' - Value not valid for PropertyType";
                        status.ObjectGuid = newData;
                        status.Code = "InvalidValue";
                    }

                }
                else
                {
                    status.Success = false;
                    status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' has invalid data";
                    status.MessageDetails += $"Unable to match data with any nodes: {currData.ToString()}\n";
                    status.Code = "InvalidValue";
                }

                return status;
            }

            if (Property.PropertyEditorAlias == "Umbraco.RelatedLinks2")
            {
                var updatedLinks = new List<RelatedLinkModel>();
                var qtyUpdated = 0;
                var currLinks = JsonConvert.DeserializeObject<IEnumerable<Umbraco.Web.Models.RelatedLink>>(currData.ToString());

                foreach (var currLink in currLinks)
                {
                    if (!currLink.IsInternal)
                    {
                        //external link - just add to list
                        var newLink = new RelatedLinkModel(currLink);
                        updatedLinks.Add(newLink);
                    }
                    else if (currLink.Link.StartsWith("umb:"))
                    {
                        //already converted - just add to list
                        var newLink = new RelatedLinkModel(currLink);
                        updatedLinks.Add(newLink);
                        qtyUpdated++;
                    }
                    else
                    {
                        var intId = 0;
                        var isInt = Int32.TryParse(currLink.Link, out intId);
                        if (isInt)
                        {
                            var udi = ConvertIdToUdi(type, intId).ToString();

                            var newLink = new RelatedLinkModel(currLink);
                            newLink.link = udi;
                            newLink.Internal = udi;
                            updatedLinks.Add(newLink);
                            qtyUpdated++;
                        }
                        else
                        {
                            status.Success = false;
                            status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' has invalid data";
                            status.MessageDetails += $"Not an int: {currData.ToString()}\n";
                            status.Code = "NotInteger";
                        }
                    }
                }

                if (qtyUpdated > 0)
                {
                    //we have made changes, update Content node
                    var newData = JsonConvert.SerializeObject(updatedLinks);
                    var valIsValid = Property.IsPropertyValueValid(newData);
                    if (valIsValid)
                    {
                        ContentNode.SetValue(propAlias, newData);
                        var saveStatus = _contentService.SaveAndPublishWithStatus(ContentNode);

                        if (saveStatus.Success)
                        {
                            status.Success = true;
                            status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' Data Updated to '{newData}'";
                            status.Code = "Updated";
                        }
                        else
                        {
                            status.Success = false;
                            status.Message = $"Unable to update Node #{ContentNode.Id} Property '{propAlias}' to '{newData}'";
                            status.Code = "SaveError";
                            status.RelatedException = saveStatus.Exception;
                        }
                    }
                    else
                    {
                        status.Success = false;
                        status.Message = $"Unable to update Node #{ContentNode.Id} Property '{propAlias}' to '{newData}' - Value not valid for PropertyType";
                        status.Code = "InvalidValue";
                    }
                }
                else
                {
                    status.Success = true;
                    status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' Data doesn't need to be updated";
                    status.ObjectGuid = currData.ToString();
                    status.Code = "NoUpdateNeeded";

                    return status;
                }

                return status;
            }

            //Check for valid type 
            if (type == "?")
            {
                status.Success = false;
                status.Message = $"Unable to get a Valid type for the Udi for Node #{ContentNode.Id}  Property '{Property.Alias}'";
                status.Code = "NoType";

                return status;
            }
            //Single or Multiple?
            if (currData.ToString().Contains(","))
            {
                //multiple values
                var newData = new List<Udi>();
                var readyToSave = true;
                var ids = currData.ToString().Split(',');
                foreach (var id in ids)
                {
                    var intId = 0;
                    var isInt = Int32.TryParse(id, out intId);
                    if (isInt)
                    {
                        var udi = ConvertIdToUdi(type, intId);
                        newData.Add(udi);
                    }
                    else
                    {
                        status.Success = false;
                        status.Message = $"Node #{ContentNode.Id} Multi-Value Property '{propAlias}' has invalid data";
                        status.MessageDetails += $"Not an int: {id}\n";
                        status.Code = "NotInteger";
                        readyToSave = false;
                    }
                }

                status.ObjectGuid = string.Join(", ", newData.Select(n => n.ToString()));

                if (readyToSave)
                {
                    var doMultiSave = false;
                    if (doMultiSave)
                    {
                        ContentNode.SetValue(propAlias, newData);
                        var saveStatus = _contentService.SaveAndPublishWithStatus(ContentNode);

                        if (saveStatus.Success)
                        {
                            status.Success = true;
                            status.Message = $"Node #{ContentNode.Id} Multi-Value Property '{propAlias}' Data TO UPDATE";

                            status.Code = "Updated";
                            status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' Data Updated to '{status.ObjectGuid}'";
                            status.Code = "Updated";
                        }
                        else
                        {
                            status.Success = false;
                            status.Message = $"Unable to update Node #{ContentNode.Id} Property '{propAlias}' to '{status.ObjectGuid}'";
                            status.Code = "SaveError";
                            status.RelatedException = saveStatus.Exception;
                        }
                    }
                    else
                    {
                        status.Success = false;
                        status.Message = $"Not Implemented - Won't update Node #{ContentNode.Id}  Property '{propAlias}' to '{status.ObjectGuid}'";
                        status.Code = "NotImplemented";
                    }
                }
            }
            else
            {
                //single
                var intId = 0;
                var isInt = Int32.TryParse(currData.ToString(), out intId);
                if (isInt)
                {
                    var udi = ConvertIdToUdi(type, intId);
                    status.ObjectGuid = udi.ToString();

                    var newData = udi.ToString();

                    var valIsValid = Property.IsPropertyValueValid(newData);
                    if (valIsValid)
                    {
                        ContentNode.SetValue(propAlias, newData);
                        var saveStatus = _contentService.SaveAndPublishWithStatus(ContentNode);

                        if (saveStatus.Success)
                        {
                            status.Success = true;
                            status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' Data Updated to '{newData}'";
                            status.Code = "Updated";
                        }
                        else
                        {
                            status.Success = false;
                            status.Message = $"Unable to update Node #{ContentNode.Id}  Property '{propAlias}' to '{newData}'";
                            status.Code = "SaveError";
                            status.RelatedException = saveStatus.Exception;
                        }
                    }
                    else
                    {
                        status.Success = false;
                        status.Message = $"Unable to update Node #{ContentNode.Id}  Property '{propAlias}' to '{newData}' - Value not valid for PropertyType";
                        status.Code = "InvalidValue";
                    }
                }
                else
                {
                    status.Success = false;
                    status.Message = $"Node #{ContentNode.Id} Property '{propAlias}' has invalid data";
                    status.MessageDetails += $"Not an int: {currData.ToString()}\n";
                    status.Code = "NotInteger";
                }
            }


            return status;
        }

        private IEnumerable<Udi> GetUdisFromUnknownIntData(string IdData)
        {
            var newUdis = new List<Udi>();
            //Is it a single Int?
            var intId = 0;
            var isInt = Int32.TryParse(IdData, out intId);
            if (isInt)
            {
                //Single Id
                var newUdi = GetUdiViaInt(intId);
                if (newUdi != null)
                {
                    newUdis.Add(newUdi);
                }
            }
            else
            {
                //Maybe it's a CSV of Ids?
                if (IdData.Contains(","))
                {
                    var ids = IdData.Split(',');
                    foreach (var id in ids)
                    {
                        var intId2 = 0;
                        var isInt2 = Int32.TryParse(id, out intId2);
                        if (isInt2)
                        {
                            var newUdi = GetUdiViaInt(intId2);
                            if (newUdi != null)
                            {
                                newUdis.Add(newUdi);
                            }
                        }
                    }
                }
            }

            return newUdis;
        }

        private Udi GetUdiViaInt(int Id)
        {
            //First check cache (for Published nodes)
            var contentNode = _umbracoHelper.TypedContent(Id);
            if (contentNode != null)
            {
                return new GuidUdi("document", contentNode.GetKey());
            }

            var mediaNode = _umbracoHelper.TypedMedia(Id);
            if (mediaNode != null)
            {
                return new GuidUdi("media", mediaNode.GetKey());
            }

            var memberNode = _umbracoHelper.TypedMember(Id);
            if (memberNode != null)
            {
                return new GuidUdi("member", memberNode.GetKey());
            }

            //Not found, check Services
            var contentContent = _contentService.GetById(Id);
            if (contentContent != null)
            {
                return new GuidUdi("document", contentContent.Key);
            }

            var mediaContent = _mediaService.GetById(Id);
            if (mediaContent != null)
            {
                return new GuidUdi("media", mediaContent.Key);
            }

            var memberContent = _memberService.GetById(Id);
            if (memberContent != null)
            {
                return new GuidUdi("member", memberContent.Key);
            }

            //If we get here, it wasn't found anywhere...
            return null;
        }

        internal Udi ConvertIdToUdi(string Type, int CurrentId)
        {
            switch (Type)
            {
                case "document":
                    var contentNode = _umbracoHelper.TypedContent(CurrentId);
                    return new GuidUdi(Type, contentNode.GetKey());
                    break;

                case "media":
                    var mediaNode = _umbracoHelper.TypedMedia(CurrentId);
                    return new GuidUdi(Type, mediaNode.GetKey());
                    break;

                case "member":
                    var memberNode = _umbracoHelper.TypedMember(CurrentId);
                    return new GuidUdi(Type, memberNode.GetKey());
                    break;

                default:
                    return null;
                    break;
            }
        }

        internal string GetNodeTypeForProperty(PropertyType Prop)
        {
            //Get TYPE
            var type = "document";
            if (Prop.PropertyEditorAlias == "Umbraco.MemberPicker2")
            {
                type = "member";
            }
            else if (Prop.PropertyEditorAlias == "Umbraco.MediaPicker2")
            {
                type = "media"; //may or may not be multiple
            }
            else if (Prop.PropertyEditorAlias == "Umbraco.MultiNodeTreePicker2")
            {
                //need to figure out NodeType - can't access the option data on the datatype, though...
                //var dataTypeId = Prop.DataTypeDefinitionId;
                //var datatype = _dataTypeService.GetDataTypeDefinitionById(dataTypeId);

                type = "?";
            }

            return type;
        }
        internal IEnumerable<DocTypeWithProperty> GetAllDocTypesWithProps()
        {
            var allDocTypes = _contentTypeService.GetAllContentTypes();
            var allDocTypesWithProps = new List<DocTypeWithProperty>();
            foreach (var docType in allDocTypes)
            {
                //Get all regular props
                foreach (var prop in docType.PropertyTypes)
                {
                    var item = new DocTypeWithProperty(docType, prop);
                    allDocTypesWithProps.Add(item);
                }
                //Get all composition props
                //foreach (var prop in docType.CompositionPropertyTypes)
                //{
                //    var item = new DocTypeWithProperty(docType.Alias, prop);
                //    allDocTypesWithProps.Add(item);
                //}

            }

            return allDocTypesWithProps;
        }


        internal Dictionary<IContentType, IEnumerable<DocTypeWithProperty>> GetGroupedDocTypesWithProps(int DataTypeId)
        {
            var returnList = new Dictionary<IContentType,IEnumerable<DocTypeWithProperty>>();

            var allDocTypesWithProps = GetAllDocTypesWithProps();
            var docTypesWithDt = allDocTypesWithProps.Where(n => n.Property.DataTypeDefinitionId == DataTypeId).ToList();
            var groups = docTypesWithDt.GroupBy(n => n.ContentType);

            foreach (var dtGroup in groups)
            {
                var docType = dtGroup.Key;
                var props = new List<DocTypeWithProperty>();

                foreach (var item in dtGroup)
                {
                    props.Add(item);
                }
                
                returnList.Add(docType, props);
            }

            return returnList;
        }
    }
}
