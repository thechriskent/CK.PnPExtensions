using OfficeDevPnP.Core.Framework.Provisioning.Providers;
using OfficeDevPnP.Core.Framework.Provisioning.Providers.Xml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace CK.PnPExtensions
{
    public class XMLTransform : ITemplateProviderExtension
    {
        public bool SupportsGetTemplatePostProcessing
        {
            get { return (false); }
        }

        public bool SupportsGetTemplatePreProcessing
        {
            get { return (false); }
        }

        public bool SupportsSaveTemplatePostProcessing
        {
            get { return (true); }
        }

        public bool SupportsSaveTemplatePreProcessing
        {
            get { return (false); }
        }


        private string _configXmlPath;

        public void Initialize(object settings)
        {
            //settings is expected to be a filepath to an XML file
            _configXmlPath = settings as string;
        }

        public OfficeDevPnP.Core.Framework.Provisioning.Model.ProvisioningTemplate PostProcessGetTemplate(OfficeDevPnP.Core.Framework.Provisioning.Model.ProvisioningTemplate template)
        {
            throw new NotImplementedException();
        }

        public System.IO.Stream PostProcessSaveTemplate(System.IO.Stream stream)
        {
            MemoryStream result = new MemoryStream();

            //Load up the Template Stream to an XmlDocument so that we can manipulate it directly
            XmlDocument doc = new XmlDocument();
            doc.Load(stream);
            XmlNamespaceManager nspMgr = new XmlNamespaceManager(doc.NameTable);
            nspMgr.AddNamespace("pnp", XMLConstants.PROVISIONING_SCHEMA_NAMESPACE_2016_05);

            //Load up the transform XML from the filesystem
            XmlDocument actions = new XmlDocument();
            actions.Load(_configXmlPath);
            XmlNamespaceManager nspMgrA = new XmlNamespaceManager(doc.NameTable);
            nspMgrA.AddNamespace("pnp", XMLConstants.PROVISIONING_SCHEMA_NAMESPACE_2016_05);

            Dictionary<string, string> tokens = DecodeTokens(actions.SelectSingleNode("//tokens"),doc,nspMgr);

            XmlNode actionsNode = actions.SelectSingleNode("//actions", nspMgrA);
            actionsNode.InnerXml = DecodeString(tokens, actionsNode.InnerXml);

            //The transform file is expected to contain a series of actions that are applied in order (so we loop through them)
            foreach (XmlNode action in actions.SelectSingleNode("//actions",nspMgrA).ChildNodes)
            {
                if (action.NodeType == XmlNodeType.Element)
                {
                    //every action should have a path to identify the element(s) to apply the transform action to
                    XmlAttribute path = action.Attributes["path"];
                    if (path != null && !string.IsNullOrEmpty(path.Value))
                    {
                        //Now we go get that element(s) from the template (if not found, we just move on)
                        XmlNodeList targetNodes = doc.SelectNodes(path.Value, nspMgr);
                        if (targetNodes != null && targetNodes.Count > 0)
                        {
                            switch (action.LocalName.ToLower())
                            {
                                case "add":
                                    string locationValue = "append";
                                    XmlAttribute location = action.Attributes["location"];
                                    if (location != null && !string.IsNullOrEmpty(location.Value))
                                    {
                                        locationValue = location.Value;
                                    }
                                    //add actions append any child elements of the action to the targeted element
                                    foreach (XmlNode entry in action.ChildNodes)
                                    {
                                        foreach (XmlNode target in targetNodes)
                                        {
                                            //We import the node to both copy it and make it possible to copy from one doc to another
                                            XmlNode importedNode = doc.ImportNode(entry, true);
                                            switch (locationValue.ToLower())
                                            {
                                                case "prepend":
                                                    target.PrependChild(importedNode);
                                                    break;
                                                case "before":
                                                    target.ParentNode.InsertBefore(importedNode, target);
                                                    break;
                                                case "after":
                                                    target.ParentNode.InsertAfter(importedNode, target);
                                                    break;
                                                default:
                                                    target.AppendChild(importedNode);
                                                    break;
                                            }
                                            
                                        }
                                    }

                                    break;
                                case "remove":
                                    //remove actions simply remove the targeted element(s) from the template
                                    foreach (XmlNode target in targetNodes)
                                    {
                                        target.ParentNode.RemoveChild(target);
                                    }
                                    break;
                                case "attribute":
                                    //attribute actions can either add, set, or remove an attribute from the targeted element(s)
                                    // in addition to path, the name attribute is required. This is the name of the attribute to be used in the targeted element(s)
                                    // the actions value attribute determines how the targeted element(s) attribute is processed
                                    //   missing attribute value results in the removal of the attribute from the targeted element(s) if they exist
                                    //   if the targeted element(s) has the attribute, the value is set
                                    //   if the targeted element(s) don't have the attribute, they get it
                                    XmlAttribute attrNameNode = action.Attributes["name"];
                                    XmlAttribute attrValueNode = action.Attributes["value"];
                                    if (attrNameNode != null)
                                    {
                                        string attrName = attrNameNode.Value;
                                        foreach (XmlNode target in targetNodes)
                                        {
                                            XmlAttribute targetAttr = target.Attributes[attrName];
                                            if (targetAttr != null)
                                            {
                                                if (attrValueNode == null)
                                                {
                                                    //attribute found in the targeted element, but the action value is missing so Remove it!
                                                    target.Attributes.RemoveNamedItem(attrName);
                                                }
                                                else
                                                {
                                                    //attribute found in the targeted element, so override it's value with the action value
                                                    targetAttr.Value = attrValueNode.Value;
                                                }
                                            }
                                            else if (attrValueNode != null)
                                            {
                                                //attribute not found in the targeted element, so add it and set the value to the action value
                                                targetAttr = doc.CreateAttribute(attrName);
                                                targetAttr.Value = attrValueNode.Value;
                                                target.Attributes.Append(targetAttr);
                                            }
                                        }
                                    }
                                    break;
                                case "reorder":
                                    //reorder actions are used to reorder the chidren of the targeted element(s)
                                    // in addition to path, the key attribute is required. This is the name of the attribute to be used to reorder the children
                                    // the order attribute is also required and uses the values of the key attribute to determine order
                                    // an optional position attribute (default = top) determines where the ordered items are placed in relation to the unordered
                                    //   top = ordered items are placed at the top and any unnamed (unordered) elements are placed in their original order afterwards
                                    //   bottom = ordered items are placed below any unnamed (unordered) elements which remain in their original order
                                    XmlAttribute attrKeyNode = action.Attributes["key"];
                                    XmlAttribute attrOrderNode = action.Attributes["order"];
                                    XmlAttribute attrPositionNode = action.Attributes["position"];
                                    string positionValue = "top";
                                    if (attrPositionNode != null)
                                    {
                                        positionValue = attrPositionNode.Value;
                                    }
                                    if (attrKeyNode != null && attrOrderNode != null)
                                    {
                                        string attrKeyValue = attrKeyNode.Value;
                                        string attrOrderValue = attrOrderNode.Value;
                                        if (!String.IsNullOrEmpty(attrKeyValue) && !String.IsNullOrEmpty(attrOrderValue))
                                        {
                                            string[] orderedValues = attrOrderValue.Split(',');
                                            if (positionValue == "top")
                                            {
                                                //need to reverse the order so that we can use prepend
                                                orderedValues = orderedValues.Reverse().ToArray();
                                            }
                                            foreach (XmlNode target in targetNodes)
                                            {
                                                if (target.ChildNodes.Count > 0)
                                                {
                                                    foreach (string orderedValue in orderedValues)
                                                    {
                                                        XmlNodeList matchingChildren = target.SelectNodes(string.Format("./*[@{0}='{1}']", attrKeyValue, orderedValue));
                                                        if (matchingChildren != null && matchingChildren.Count > 0)
                                                        {
                                                            foreach (XmlNode matchedChild in matchingChildren)
                                                            {
                                                                //remove each found element (so we can add it back in the right order)
                                                                target.RemoveChild(matchedChild);
                                                            }
                                                            if (positionValue == "top")
                                                            {
                                                                //cycle in reverse order to preserve order of duplicate value ordered items
                                                                for (int m = matchingChildren.Count-1; m >= 0; m--)
                                                                {
                                                                    target.PrependChild(matchingChildren[m]);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                foreach (XmlNode matchedChild in matchingChildren)
                                                                {
                                                                    //slap it on the end!
                                                                    target.AppendChild(matchedChild);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }

            //Put it back into stream form for other provider extensions to have a go and to finish processing
            doc.Save(result);
            result.Position = 0;

            return (result);
        }

        public System.IO.Stream PreProcessGetTemplate(System.IO.Stream stream)
        {
            throw new NotImplementedException();
        }

        public OfficeDevPnP.Core.Framework.Provisioning.Model.ProvisioningTemplate PreProcessSaveTemplate(OfficeDevPnP.Core.Framework.Provisioning.Model.ProvisioningTemplate template)
        {
            throw new NotImplementedException();
        }


        public Dictionary<string, string> DecodeTokens(XmlNode tokensNode, XmlDocument template, XmlNamespaceManager nspMgr)
        {
            Dictionary<string,string> tokens = new Dictionary<string, string>();
            if (tokensNode != null && tokensNode.ChildNodes.Count > 0)
            {
                XmlAttribute startNode = tokensNode.Attributes["start"];
                string startValue = "_.";
                if (startNode != null) { startValue = startNode.Value; }
                XmlAttribute endNode = tokensNode.Attributes["end"];
                string endValue = "._";
                if (endNode != null) { endValue = endNode.Value; }

                foreach (XmlNode token in tokensNode)
                {
                    XmlAttribute nameNode = token.Attributes["name"];
                    if(nameNode != null && !String.IsNullOrEmpty(nameNode.Value))
                    {
                        string nameValue = startValue + nameNode.Value + endValue;
                        string tokenType = token.LocalName;

                        XmlAttribute pathNode = token.Attributes["path"];
                        XmlAttribute valueNode = token.Attributes["value"];
                        XmlAttribute comparetoNode = token.Attributes["compareto"];
                        
                        //calculate tokenValueValue
                        string tokenValueValue = String.Empty;
                        if (valueNode != null)
                        {
                            tokenValueValue = DecodeString(tokens, valueNode.Value);
                        }

                        //calculate tokenPathValue
                        string tokenPathValue = String.Empty;
                        if (pathNode != null)
                        {
                            string pathValue = pathNode.Value;
                            XmlNodeList tokenPathNodes = template.SelectNodes(pathValue, nspMgr);
                            if (tokenPathNodes != null && tokenPathNodes.Count > 0)
                            {
                                //if more than one returned, just take the first
                                XmlNode tokenPathNode = tokenPathNodes[0];
                                if (tokenPathNode.NodeType == XmlNodeType.Attribute)
                                {
                                    tokenPathValue = tokenPathNode.Value;
                                }
                                else
                                {
                                    bool includeselfValue = false;
                                    XmlAttribute includeselfNode = token.Attributes["includeself"];
                                    if (includeselfNode != null && includeselfNode.Value == "true") { includeselfValue = true; }
                                    if (includeselfValue)
                                    {
                                        tokenPathValue = tokenPathNode.OuterXml;
                                    }
                                    else
                                    {
                                        tokenPathValue = tokenPathNode.InnerXml;
                                    }
                                }
                            }
                        }
                        tokenPathValue = DecodeString(tokens, tokenPathValue);

                        //calculate tokenComparisonValue
                        string tokenComparisonValue = String.Empty;
                        if (comparetoNode != null)
                        {
                            if (pathNode != null) { tokenComparisonValue = tokenPathValue; }
                            else if (valueNode != null) { tokenComparisonValue = tokenValueValue; }
                        }

                        //calculate tokenComparetoValue
                        string tokenComparetoValue = String.Empty;
                        if (comparetoNode != null)
                        {
                            tokenComparetoValue = DecodeString(tokens, comparetoNode.Value);
                        }


                        switch (tokenType.ToLower())
                        {
                            case "path":
                                if (pathNode != null)
                                {
                                    tokens.Add(nameValue, tokenPathValue);
                                }
                                break;
                            case "exists":
                                
                                break;
                            case "equals":
                                if (comparetoNode != null)
                                {
                                    if (tokenComparisonValue.Equals(tokenComparetoValue)) { tokens.Add(nameValue, "true"); }
                                    else { tokens.Add(nameValue, "false"); }
                                }
                                break;
                            case "contains":
                                if (comparetoNode != null)
                                {
                                    if (tokenComparisonValue.Contains(tokenComparetoValue)) { tokens.Add(nameValue, "true"); }
                                    else { tokens.Add(nameValue, "false"); }
                                }
                                break;
                            case "startswith":
                                if (comparetoNode != null)
                                {
                                    if (tokenComparisonValue.StartsWith(tokenComparetoValue)) { tokens.Add(nameValue, "true"); }
                                    else { tokens.Add(nameValue, "false"); }
                                }
                                break;
                            case "endswith":
                                if (comparetoNode != null)
                                {
                                    if (tokenComparisonValue.EndsWith(tokenComparetoValue)) { tokens.Add(nameValue, "true"); }
                                    else { tokens.Add(nameValue, "false"); }
                                }
                                break;
                            default:
                                //simple token
                                if (valueNode != null)
                                {
                                    tokens.Add(nameValue, tokenValueValue);
                                }
                                break;
                        }
                    }
                }
            }
            return tokens;
        }

        public string DecodeString(Dictionary<string, string> tokens, string encodedValue)
        {
            foreach (KeyValuePair<string,string> tokenKVP in tokens)
            {
                encodedValue = encodedValue.Replace(tokenKVP.Key, tokenKVP.Value);
            }
            return encodedValue;
        }

    }
}
