// WFS provider by Peter Robineau (peter.robineau@gmx.at)
// This file can be redistributed and/or modified under the terms of the GNU Lesser General Public License.

using System.IO;
using System.Text;
using System.Xml;
using EasyMap.Data.Providers;
using EasyMap.Geometries;

namespace EasyMap.Utilities.Wfs
{
    public class WFS_1_0_0_TextResources : WFS_1_0_0_XPathTextResources, IWFS_TextResources
    {
        #region Public Member

        ////////////////////////////////////////////////////////////////////////
        // HTTP Configuration                                                 //                      
        // POST & GET                                                         //
        ////////////////////////////////////////////////////////////////////////

        /// <summary>
        /// This method returns the query string for 'GetCapabilities'.
        /// </summary>
        public string GetCapabilitiesRequest()
        {
            return "?SERVICE=WFS&Version=1.0.0&REQUEST=GetCapabilities";
        }

        /// <summary>
        /// This method returns the query string for 'DescribeFeatureType'.
        /// </summary>
        /// <param name="featureTypeName">The name of the featuretype to query</param>
        public string DescribeFeatureTypeRequest(string featureTypeName)
        {
            return "?SERVICE=WFS&Version=1.0.0&REQUEST=DescribeFeatureType&TYPENAME=" + featureTypeName;
        }

        #endregion

        #region IWFS_TextResources Member

        /// <summary>
        /// This method returns the query string for 'GetFeature'.
        /// </summary>
        /// <param name="featureTypeInfo">A <see cref="WFS.WfsFeatureTypeInfo"/> instance providing metadata of the featuretype to query</param>
        /// <param name="boundingBox">The bounding box of the query</param>
        /// <param name="filter">An instance implementing <see cref="IFilter"/></param>
        public string GetFeatureGETRequest(WfsFeatureTypeInfo featureTypeInfo, BoundingBox boundingBox, IFilter filter)
        {
            string qualification = string.IsNullOrEmpty(featureTypeInfo.Prefix)
                                       ? string.Empty
                                       : featureTypeInfo.Prefix + ":";
            string filterString = string.Empty;

            if (filter != null)
            {
                filterString = filter.Encode();
                filterString = filterString.Replace("<", "%3C");
                filterString = filterString.Replace(">", "%3E");
                filterString = filterString.Replace(" ", "");
                filterString = filterString.Replace("*", "%2a");
                filterString = filterString.Replace("#", "%23");
                filterString = filterString.Replace("!", "%21");
            }

            StringBuilder filterBuilder = new StringBuilder();
            filterBuilder.Append("&filter=%3CFilter%20xmlns=%22" + NSOGC + "%22%20xmlns:gml=%22" + NSGML +
                                 "%22%3E%3CBBOX%3E%3CPropertyName%3E");
            filterBuilder.Append(qualification);
            filterBuilder.Append(featureTypeInfo.Geometry._GeometryName);
            filterBuilder.Append("%3C/PropertyName%3E%3Cgml:Box%20srsName=%22EPSG:" + featureTypeInfo.SRID + "%22%3E");
            filterBuilder.Append("%3Cgml:coordinates%3E");
            filterBuilder.Append(XmlConvert.ToString(boundingBox.Left) + ",");
            filterBuilder.Append(XmlConvert.ToString(boundingBox.Bottom) + "%20");
            filterBuilder.Append(XmlConvert.ToString(boundingBox.Right) + ",");
            filterBuilder.Append(XmlConvert.ToString(boundingBox.Top));
            filterBuilder.Append("%3C/gml:coordinates%3E%3C/gml:Box%3E%3C/BBOX%3E");
            filterBuilder.Append(filterString);
            filterBuilder.Append("%3C/Filter%3E");

            return "?SERVICE=WFS&Version=1.0.0&REQUEST=GetFeature&TYPENAME=" + qualification + featureTypeInfo.Name +
                   "&PROPERTYNAME=" + qualification + featureTypeInfo.Geometry._GeometryName +
                   "&SRS=" + featureTypeInfo.SRID + filterBuilder;
        }

        /// <summary>
        /// This method returns the POST request for 'GetFeature'.
        /// </summary>
        /// <param name="featureTypeInfo">A <see cref="WFS.WfsFeatureTypeInfo"/> instance providing metadata of the featuretype to query</param>
        /// <param name="labelProperty">A property necessary for label rendering</param>
        /// <param name="boundingBox">The bounding box of the query</param>
        /// <param name="filter">An instance implementing <see cref="IFilter"/></param>
        public byte[] GetFeaturePOSTRequest(WfsFeatureTypeInfo featureTypeInfo, string labelProperty,
                                            BoundingBox boundingBox, IFilter filter)
        {
            string qualification = string.IsNullOrEmpty(featureTypeInfo.Prefix)
                                       ? string.Empty
                                       : featureTypeInfo.Prefix + ":";

            using (StringWriter sWriter = new StringWriter())
            {
                using (XmlTextWriter xWriter = new XmlTextWriter(sWriter))
                {
                    xWriter.Namespaces = true;
                    xWriter.WriteStartElement("GetFeature", NSWFS);
                    xWriter.WriteAttributeString("service", "WFS");
                    xWriter.WriteAttributeString("version", "1.0.0");
                    xWriter.WriteStartElement("Query", NSWFS);
                    xWriter.WriteAttributeString("typeName", qualification + featureTypeInfo.Name);
                    xWriter.WriteElementString("PropertyName", qualification + featureTypeInfo.Geometry._GeometryName);
                    if (!string.IsNullOrEmpty(labelProperty))
                        xWriter.WriteElementString("PropertyName", qualification + labelProperty);
                    xWriter.WriteStartElement("Filter", NSOGC);
                    xWriter.WriteStartElement("BBOX");
                    xWriter.WriteElementString("PropertyName", featureTypeInfo.Geometry._GeometryName);
                    xWriter.WriteStartElement("gml", "Box", NSGML);
                    xWriter.WriteAttributeString("srsName",
                                                 "http://www.opengis.net/gml/srs/epsg.xml#" + featureTypeInfo.SRID);
                    xWriter.WriteElementString("coordinates", NSGML,
                                               XmlConvert.ToString(boundingBox.Left) + "," +
                                               XmlConvert.ToString(boundingBox.Bottom) + " " +
                                               XmlConvert.ToString(boundingBox.Right) + "," +
                                               XmlConvert.ToString(boundingBox.Top));
                    xWriter.WriteEndElement();
                    xWriter.WriteEndElement();
                    if (filter != null) xWriter.WriteRaw(filter.Encode());
                    if (filter != null) xWriter.WriteEndElement();
                    xWriter.WriteEndElement();
                    xWriter.WriteEndElement();
                    xWriter.WriteEndElement();
                    xWriter.Flush();
                    return Encoding.UTF8.GetBytes(sWriter.ToString());
                }
            }
        }

        #endregion
    }
}