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
                        if (targetNodes != null)
                        {
                            switch (action.LocalName)
                            {
                                case "add":
                                    //add actions append any child elements of the action to the targeted element
                                    foreach (XmlNode entry in action.ChildNodes)
                                    {
                                        foreach (XmlNode target in targetNodes)
                                        {
                                            //We import the node to both copy it and make it possible to copy from one doc to another
                                            XmlNode importedNode = doc.ImportNode(entry, true);
                                            target.AppendChild(importedNode);
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
                                    // the actions value (innertext) determines how the targeted element(s) attribute is processed
                                    //   blank values result in the removal of the attribute from the targeted element(s) if they exist
                                    //   if the targeted element(s) has the attribute, the value is set
                                    //   if the targeted element(s) don't have the attribute, they get it
                                    XmlAttribute attrNameNode = action.Attributes["name"];
                                    if (attrNameNode != null)
                                    {
                                        string attrName = attrNameNode.Value;
                                        string attrValue = action.InnerText;
                                        foreach (XmlNode target in targetNodes)
                                        {
                                            XmlAttribute targetAttr = target.Attributes[attrName];
                                            if (targetAttr != null)
                                            {
                                                if (string.IsNullOrEmpty(attrValue))
                                                {
                                                    //attribute found in the targeted element, but the action value is nothing so Remove it!
                                                    target.Attributes.RemoveNamedItem(attrName);
                                                }
                                                else
                                                {
                                                    //attribute found in the targeted element, so override it's value with the action value
                                                    targetAttr.Value = attrValue;
                                                }
                                            }
                                            else if (!string.IsNullOrEmpty(attrValue))
                                            {
                                                //attribute not found in the targeted element, so add it and set the value to the action value
                                                targetAttr = doc.CreateAttribute(attrName);
                                                targetAttr.Value = attrValue;
                                                target.Attributes.Append(targetAttr);
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

        
    }
}
