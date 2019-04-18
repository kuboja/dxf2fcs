﻿using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dxf2fcs
{
    public class DxfLoader
    {
        private const double unit = 0.001;
        private bool oldVertex = false;
        private int precision = 5;

        private StringBuilder sb;
        private int i;
        private Matrix3 trans;
        private Vector3 translation;

        public string ToFcs(string dxfFilePath)
        {
            var doc = DxfDocument.Load(dxfFilePath);

            sb = new StringBuilder();
            i = 0;

            sb.AppendLine("cv = ar => ar.Select(v => { Vertex = Fcs.Geometry.Vertex3D(v[0], v[1], v[2]), Radius = 0 } )");
            sb.AppendLine("v = x,y,z => Fcs.Geometry.Vertex3D(x,y,z)");

            foreach (var insert in doc.Inserts)
            {
                var block = insert.Block;
                trans = insert.GetTransformation(netDxf.Units.DrawingUnits.Millimeters);
                translation = insert.Position;
                EntitiesToFcs(block.Entities);
            }

            trans = Matrix3.Identity;
            translation = Vector3.Zero;

            EntitiesToFcs(doc.Lines);
            EntitiesToFcs(doc.Arcs);
            EntitiesToFcs(doc.Circles);
            EntitiesToFcs(doc.Ellipses);
            EntitiesToFcs(doc.Points);
            EntitiesToFcs(doc.Splines);
            EntitiesToFcs(doc.Traces);
            EntitiesToFcs(doc.Texts);
            EntitiesToFcs(doc.Solids);
            EntitiesToFcs(doc.Shapes);
            EntitiesToFcs(doc.Polylines);
            EntitiesToFcs(doc.PolyfaceMeshes);
            EntitiesToFcs(doc.Meshes);
            EntitiesToFcs(doc.MTexts);
            EntitiesToFcs(doc.MLines);
            EntitiesToFcs(doc.LwPolylines);
            EntitiesToFcs(doc.Faces3d);
            EntitiesToFcs(doc.Images);
            //EntitiesToFcs(doc.Groups);

            return sb.ToString();
        }

        private void EntitiesToFcs(IEnumerable<EntityObject> entities)
        {
            {
                foreach (var item in entities)
                {
                    i++;
                    switch (item)
                    {
                        case Line l:
                            DrawLine(l);
                            break;
                        case Spline l:
                            DrawSpline(l);
                            break;
                        case Arc a:
                            DrawArc(a);
                            break;
                        case Circle c:
                            DrawCircle(c);
                            break;
                        case Ellipse e:
                            DrawElipse(e);
                            break;
                        default:
                            Console.WriteLine($"Nepodporovaná entita: {item.GetType()}");
                            break;
                    }
                }
            }
        }

        private void DrawElipse(Ellipse e)
        {
            var elipseLines = e.ToPolyline(4).Explode();

            var A = (elipseLines[0] as Line).StartPoint;
            var B = (elipseLines[1] as Line).EndPoint;
            var C = (elipseLines[1] as Line).StartPoint;
            var D = (elipseLines[3] as Line).StartPoint;

            sb.AppendLine(Vector3ToFcsVertex($"v{i}a", A));
            sb.AppendLine(Vector3ToFcsVertex($"v{i}b", B));
            sb.AppendLine(Vector3ToFcsVertex($"v{i}c", C));
            sb.AppendLine(Vector3ToFcsVertex($"v{i}d", D));

            sb.AppendLine($"curve {{c{i}a}} arc vertex v{i}a v{i}c v{i}b ");
            sb.AppendLine($"curve {{c{i}b}} arc vertex v{i}b v{i}d v{i}a ");
        }

        private void DrawCircle(Circle c)
        {
            var circleLines = c.ToPolyline(4).Explode();

            var A = (circleLines[0] as Line).StartPoint;
            var B = (circleLines[1] as Line).EndPoint;
            var C = (circleLines[1] as Line).StartPoint;
            var D = (circleLines[3] as Line).StartPoint;

            sb.AppendLine(Vector3ToFcsVertex($"v{i}a", A));
            sb.AppendLine(Vector3ToFcsVertex($"v{i}b", B));
            sb.AppendLine(Vector3ToFcsVertex($"v{i}c", C));
            sb.AppendLine(Vector3ToFcsVertex($"v{i}d", D));

            sb.AppendLine($"curve {{c{i}a}} arc vertex v{i}a v{i}c v{i}b ");
            sb.AppendLine($"curve {{c{i}b}} arc vertex v{i}b v{i}d v{i}a ");
        }

        private void DrawArc(Arc a)
        {
            var explodedEntities = a.ToPolyline(2).Explode();

            var A = (explodedEntities[0] as Line).StartPoint;
            var B = (explodedEntities[1] as Line).EndPoint;
            var C = (explodedEntities[1] as Line).StartPoint;

            sb.AppendLine(Vector3ToFcsVertex($"v{i}a", A));
            sb.AppendLine(Vector3ToFcsVertex($"v{i}b", B));
            sb.AppendLine(Vector3ToFcsVertex($"v{i}c", C));

            sb.AppendLine($"curve {{c{i}}} arc vertex v{i}a v{i}c v{i}b");
        }

        private void DrawSpline(Spline l)
        {
            sb.AppendLine($"vs{i} = [");
            sb.AppendJoin(",\n", l.ControlPoints.Select(v => Vector3ToFcsArray(trans * v.Position + translation)));
            sb.AppendLine($"]");

            sb.AppendLine($"curve {{c{i}}} filletedpoly items (cv(vs{i}))");
        }

        private void DrawLine(Line l)
        {
            sb.AppendLine(Vector3ToFcsVertex($"v{i}a", l.StartPoint));
            sb.AppendLine(Vector3ToFcsVertex($"v{i}b", l.EndPoint));

            sb.AppendLine($"curve {{c{i}}} vertex v{i}a v{i}b");
        }

        private string Vector3ToFcsVertex(string name, Vector3 vLocal)
        {
            var v = Transform(vLocal) * unit;
            if (oldVertex)
                return $"vertex {{{name}}} xyz {Math.Round(v.X, precision)} {Math.Round(v.Y, precision)} {Math.Round(v.Z, precision)}";
            else
                return $"{name} = v({Math.Round(v.X, precision)},{Math.Round(v.Y, precision)},{Math.Round(v.Z, precision)})";
        }
        private string Vector3ToFcsArray(Vector3 vLocal)
        {
            var v = Transform(vLocal) * unit;
            
            return $"[{Math.Round(v.X,precision)},{Math.Round(v.Y, precision)},{Math.Round(v.Z,precision)}]";
        }

        private Vector3 Transform(Vector3 point)
        {
            return trans * point + translation;
        }
    }
}
