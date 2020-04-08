

namespace Dragonfly.Umbraco7ObsoletePickerConverter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Dragonfly.NetModels;
    using Dragonfly.Umbraco7ObsoletePickerConverter.Models;
    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Services;
    using Umbraco.Web;

    internal class PickerFixService
    {
        IContentService _contentService;
        IDataTypeService _dataTypeService;
        IContentTypeService _contentTypeService;
        UmbracoHelper _umbracoHelper;
        public PickerFixService(ServiceContext Services)
        {
            _contentService = Services.ContentService;
            _dataTypeService = Services.DataTypeService;
            _contentTypeService = Services.ContentTypeService;
            _umbracoHelper = new UmbracoHelper(UmbracoContext.Current);
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

        internal StatusMessage DoUpdatePickerData(IDataTypeDefinition DataType)
        {
            var mainStatus = new StatusMessage();

            var dt = DataType;

            if (PropertyEditorTypesNewToCheck().Contains(dt.PropertyEditorAlias))
            {
                //TODO: Do stuff
                var allDocTypesWithProps = GetAllDocTypesWithProps();
                var docTypesWithDt =
                    allDocTypesWithProps.Where(n => n.Property.PropertyEditorAlias == dt.PropertyEditorAlias);

                var groups = docTypesWithDt.GroupBy(n => n.ContentType);
                foreach (var dtGroup in groups)
                {
                    var docType = dtGroup.Key;
                    var props = new List<DocTypeWithProperty>();

                    foreach (var item in dtGroup)
                    {
                        props.Add(item);
                    }

                    mainStatus.MessageDetails += $"Doctype '{docType.Alias}':\n";
                    mainStatus.MessageDetails += $"-Props: {string.Join(", ", props.Select(n => n.Property.Alias))}\n";

                    var nodesToUpdate = _contentService.GetContentOfContentType(docType.Id);
                    mainStatus.MessageDetails += $"-Nodes '{nodesToUpdate.Count()}':\n";
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
                                    var nodeUpdateStatus = DoUpdateNodePropertyData(node, prop.Property);
                                    var udi = nodeUpdateStatus.ObjectGuid;
                                    nodeMsg += $"  NEW UDI = {udi} ";
                                }
                            }


                        }

                        mainStatus.MessageDetails += $"{nodeMsg}'\n";
                    }


                }

                //var allContentNodes = 0;
                //var nodesToUpdate

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

        internal StatusMessage DoUpdateNodePropertyData(IContent ContentNode, PropertyType Property)
        {
            var status = new StatusMessage();

            var propAlias = Property.Alias;
            var currData = ContentNode.GetValue(propAlias);

            if (currData.ToString().StartsWith("umb:"))
            {
                //we have a UDI
                status.Success = true;
                status.Message = "Property Data is already a UDI";
                status.ObjectGuid = currData.ToString();
                status.Code = "NoUpdateNeeded";
            }
            else
            {
                var type = GetNodeTypeForProperty(Property);

                //Single or Multiple?
                if (currData.ToString().Contains(","))
                {
                    //multiple values
                    var newData = new List<Udi>();
                    var ids = currData.ToString().Split(',');
                    foreach (var id in ids)
                    {
                        var intId = 0;
                        var isInt = Int32.TryParse(id, out intId);
                        if (isInt)
                        {
                            var udi = ConvertIdToUdi(type, intId);
                            newData.Add(udi);
                            status.Success = true;
                            status.Message = "Multi-Value Property Data TO UPDATE";
                            status.ObjectGuid = string.Join(", ", newData.Select(n => n.ToString()));
                            status.Code = "Updated";
                        }
                        else
                        {
                            status.Success = false;
                            status.Message = "Multi-Value Property has invalid data";
                            status.MessageDetails += $"Not an int: {id}\n";
                            status.Code = "NotInteger";
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

                        status.Success = true;
                        status.Message = "Property Data TO UPDATE";
                        status.ObjectGuid = udi.ToString();
                        status.Code = "Updated";
                    }
                    else
                    {
                        status.Success = false;
                        status.Message = "Property has invalid data";
                        status.MessageDetails += $"Not an int: {currData.ToString()}\n";
                        status.Code = "NotInteger";
                    }
                }
            }

            return status;
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
                //need to check NodeType
                //IDataTypeService dataTypeService = Services.DataTypeService;
                //var dataType = Property.propertyTy
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

    }
}
