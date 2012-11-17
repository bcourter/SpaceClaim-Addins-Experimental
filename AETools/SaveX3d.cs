using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Linq;
using SpaceClaim.Api.V10;
using SpaceClaim.Api.V10.Extensibility;
using SpaceClaim.Api.V10.Geometry;
using SpaceClaim.Api.V10.Modeler;
using SpaceClaim.Api.V10.Display;
using SpaceClaim.AddInLibrary;
using SpaceClaim.Svg;
using AETools.Properties;
using Color = System.Drawing.Color;

namespace SpaceClaim.AddIn.AETools {

    class X3dFileSaveHandler : FileSaveHandler {
        public X3dFileSaveHandler()
            : base("X3D Files", "x3d") {
        }

        public override void SaveFile(string path) {
            double surfaceDeviation = 0.001;
            double angleDeviation = 1;

            XmlWriter xmlWriter;
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;

            xmlWriter = XmlWriter.Create(path, settings);

            Part mainPart = Window.ActiveWindow.Scene as Part;
            if (mainPart == null)
                return;

            xmlWriter.WriteStartDocument();

            xmlWriter.WriteComment("X3D proof of concept exporter from SpaceClaim.");
            xmlWriter.WriteDocType("X3D", "ISO//Web3D//DTD X3D 3.1//EN", "http://www.web3d.org/specifications/x3d-3.1.dtd", null);

            xmlWriter.WriteStartElement("X3D", "http://www.w3.org/2001/XMLSchema-instance");
            xmlWriter.WriteAttributeString("profile", "CADInterchange");
            xmlWriter.WriteAttributeString("version", "3.1");
            //	xmlWriter.WriteAttributeString("xsd:noNamespaceSchemaLocation",  "http://www.web3d.org/specifications/x3d-3.1.xsd"); // TBD do this correctly

            xmlWriter.WriteStartElement("head");

            xmlWriter.WriteStartElement("component");
            xmlWriter.WriteAttributeString("level", "2");
            xmlWriter.WriteAttributeString("name", "CADGeometry");
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement("meta");
            xmlWriter.WriteAttributeString("name", "title");
            xmlWriter.WriteAttributeString("content", Path.GetFileName(path));
            xmlWriter.WriteEndElement();

            xmlWriter.WriteEndElement(); // head

            xmlWriter.WriteStartElement("Scene");

            xmlWriter.WriteStartElement("Viewport");
            xmlWriter.WriteAttributeString("description", "SpaceClaim Model Viewport");
            xmlWriter.WriteAttributeString("position", "0 0 0");
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement("CADLayer");
            xmlWriter.WriteAttributeString("DEF", "OnlyLayer");
            xmlWriter.WriteAttributeString("name", "Only layer for this model");

            xmlWriter.WriteStartElement("CADAssembly");
            xmlWriter.WriteAttributeString("name", mainPart.Name);

            int appearanceIndex = 0;
            foreach (IDesignBody iDesBody in mainPart.GetDescendants<IDesignBody>()) {
                xmlWriter.WriteStartElement("CADPart");
                xmlWriter.WriteAttributeString("name", iDesBody.Master.Name);

                Matrix trans = iDesBody.TransformToMaster.Inverse;
                Color color = iDesBody.Master.GetVisibleColor();
                string colorString = string.Format("{0} {1} {2}", (float)color.R / 255, (float)color.G / 255, (float)color.B / 255);
                string appearanceString = "APP" + appearanceIndex++;

                IDictionary<Face, FaceTessellation> tessellationMap = iDesBody.Master.Shape.GetTessellation(null, FacetSense.RightHanded, new TessellationOptions(surfaceDeviation, angleDeviation));

                bool isFirstAppearance = true;
                foreach (Face face in tessellationMap.Keys) {
                    xmlWriter.WriteStartElement("CADFace");
                    xmlWriter.WriteAttributeString("name", face.ToString());

                    xmlWriter.WriteStartElement("Shape");
                    xmlWriter.WriteAttributeString("containerField", "shape");

                    xmlWriter.WriteStartElement("Appearance");

                    if (isFirstAppearance) {
                        xmlWriter.WriteAttributeString("DEF", appearanceString);
                        xmlWriter.WriteStartElement("Material");
                        xmlWriter.WriteAttributeString("diffuseColor", colorString);
                        xmlWriter.WriteEndElement();
                        isFirstAppearance = false;
                    }
                    else {
                        xmlWriter.WriteAttributeString("USE", appearanceString);
                    }

                    xmlWriter.WriteEndElement(); // Appearance

                    string coordIndexField = string.Empty;
                    FaceTessellation faceTessellation = tessellationMap[face];
                    IList<FacetVertex> vertices = faceTessellation.Vertices;
                    foreach (FacetStrip facetStrip in faceTessellation.FacetStrips) {
                        foreach (Facet facet in facetStrip.Facets) {
                            coordIndexField += string.Format("{0} {1} {2} -1 ", facet.Vertex0, facet.Vertex1, facet.Vertex2);
                        }
                    }

                    xmlWriter.WriteStartElement("IndexedFaceSet");
                    xmlWriter.WriteAttributeString("creaseAngle", "5.0");
                    xmlWriter.WriteAttributeString("coordIndex", coordIndexField);

                    string pointField = string.Empty;
                    foreach (FacetVertex vertex in vertices) {
                        Point point = trans * vertex.Position;
                        pointField += string.Format("{0} {1} {2} ", point.X, point.Y, point.Z);
                    }

                    xmlWriter.WriteStartElement("Coordinate");
                    xmlWriter.WriteAttributeString("point", pointField);
                    xmlWriter.WriteEndElement();

                    string normalField = string.Empty;
                    foreach (FacetVertex vertex in vertices) {
                        Direction normal = trans * vertex.Normal;
                        normalField += string.Format("{0} {1} {2} ", normal.X, normal.Y, normal.Z);
                    }

                    xmlWriter.WriteStartElement("Normal");
                    xmlWriter.WriteAttributeString("vector", normalField);
                    xmlWriter.WriteEndElement();

                    xmlWriter.WriteEndElement(); // IndexedFaceSet

                    xmlWriter.WriteEndElement(); // Shape
                    xmlWriter.WriteEndElement(); // CADFace
                }

                xmlWriter.WriteEndElement(); // CADPart
            }

            xmlWriter.WriteEndElement(); // CADAssembly
            xmlWriter.WriteEndElement(); // CADLayer
            xmlWriter.WriteEndElement(); // Scene
            xmlWriter.WriteEndElement(); // X3D
            xmlWriter.WriteEndDocument();
            xmlWriter.Close();
        }

    }
}
