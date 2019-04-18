using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dxf2fcs
{
    public class DxfLoader
    {
        const double unit = 0.001;

        StringBuilder sb;
        int i;
        Matrix3 trans;
        Vector3 translation;

        public string ToFcs(string dxfFilePath)
        {
            var doc = DxfDocument.Load(dxfFilePath);

            sb = new StringBuilder();
            i = 0;

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

            sb.AppendLine($"v_{i}_a = {Vector3ToFcsVertex3D(A)}");
            sb.AppendLine($"v_{i}_b = {Vector3ToFcsVertex3D(B)}");
            sb.AppendLine($"v_{i}_c = {Vector3ToFcsVertex3D(C)}");
            sb.AppendLine($"v_{i}_d = {Vector3ToFcsVertex3D(D)}");

            sb.AppendLine($"curve {{c_{i}_a}} arc vertex {{v_{i}_a}} {{v_{i}_c}} {{v_{i}_b}} ");
            sb.AppendLine($"curve {{c_{i}_b}} arc vertex {{v_{i}_b}} {{v_{i}_d}} {{v_{i}_a}} ");
        }

        private void DrawCircle(Circle c)
        {
            var circleLines = c.ToPolyline(4).Explode();

            var A = (circleLines[0] as Line).StartPoint;
            var B = (circleLines[1] as Line).EndPoint;
            var C = (circleLines[1] as Line).StartPoint;
            var D = (circleLines[3] as Line).StartPoint;

            sb.AppendLine($"v_{i}_a = {Vector3ToFcsVertex3D(A)}");
            sb.AppendLine($"v_{i}_b = {Vector3ToFcsVertex3D(B)}");
            sb.AppendLine($"v_{i}_c = {Vector3ToFcsVertex3D(C)}");
            sb.AppendLine($"v_{i}_d = {Vector3ToFcsVertex3D(D)}");

            sb.AppendLine($"curve {{c_{i}_a}} arc vertex {{v_{i}_a}} {{v_{i}_c}} {{v_{i}_b}} ");
            sb.AppendLine($"curve {{c_{i}_b}} arc vertex {{v_{i}_b}} {{v_{i}_d}} {{v_{i}_a}} ");
        }

        private void DrawArc(Arc a)
        {
            var explodedEntities = a.ToPolyline(2).Explode();

            var A = (explodedEntities[0] as Line).StartPoint;
            var B = (explodedEntities[1] as Line).EndPoint;
            var C = (explodedEntities[1] as Line).StartPoint;

            sb.AppendLine($"v_{i}_a = {Vector3ToFcsVertex3D(A)}");
            sb.AppendLine($"v_{i}_b = {Vector3ToFcsVertex3D(B)}");
            sb.AppendLine($"v_{i}_c = {Vector3ToFcsVertex3D(C)}");

            sb.AppendLine($"curve {{c_{i}}} arc vertex {{v_{i}_a}} {{v_{i}_c}} {{v_{i}_b}} ");
        }

        private void DrawSpline(Spline l)
        {
            sb.AppendLine($"vs_{i} = [");
            sb.AppendJoin(",\n", l.ControlPoints.Select(v => Vector3ToFcsArray(trans * v.Position + translation)));
            sb.AppendLine($"].Select(v => {{ Vertex = Fcs.Geometry.Vertex3D(v[0], v[1], v[2]), Radius = 0 }} )");

            sb.AppendLine($"curve {{c_{i}}} filletedpoly items (vs_{i}) radiusmultiplier 0");
        }

        private void DrawLine(Line l)
        {
            sb.AppendLine($"v_{i}_a = {Vector3ToFcsVertex3D(l.StartPoint)}");
            sb.AppendLine($"v_{i}_b = {Vector3ToFcsVertex3D(l.EndPoint)}");

            sb.AppendLine($"curve {{c_{i}}} vertex {{v_{i}_a}} {{v_{i}_b}} ");
        }

        private string Vector3ToFcsVertex3D(Vector3 vLocal)
        {
            var v = Transform(vLocal);
            return $"Fcs.Geometry.Vertex3D({v.X * unit},{v.Y * unit},{v.Z * unit})";
        }
        private string Vector3ToFcsArray(Vector3 vLocal)
        {
            var v = Transform(vLocal);
            return $"[{v.X * unit},{v.Y * unit},{v.Z * unit}]";
        }

        private Vector3 Transform(Vector3 point)
        {
            return trans * point + translation;
        }
    }
}
