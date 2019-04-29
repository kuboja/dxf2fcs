using netDxf;
using netDxf.Entities;
using netDxf.Units;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dxf2fcs
{
    public class DxfLoader
    {
        private readonly double unit;
        private readonly int precision;
        private readonly string numFormat;
        private readonly bool oldVertex = false;
        private readonly StringBuilder sb;
        private readonly Stack<Matrix3> transformations;
        private readonly Stack<Vector3> translations;

        private int i;
        private Matrix3 trans;
        private Vector3 translation;
        
        public DxfLoader(Units unit, int precision)
        {
            switch (unit)
            {
                case Units.mm:
                    this.unit = 0.001;
                    break;
                case Units.m:
                    this.unit = 1.0;
                    break;
                default:
                    break;
            }

            this.precision = precision;
            numFormat = "{0:0." + new string('#', precision) + "}";

            sb = new StringBuilder();
            transformations = new Stack<Matrix3>();
            translations = new Stack<Vector3>();
        }

        public string ToFcs(string dxfFilePath)
        {
            var doc = DxfDocument.Load(dxfFilePath);

            sb.Clear();
            i = 0;

            trans = Matrix3.Identity;
            translation = Vector3.Zero;

            transformations.Clear();
            transformations.Push(trans);
            translations.Clear();
            translations.Push(translation);

            sb.AppendLine("cv = ar => ar.Select(v => { Vertex = Fcs.Geometry.Vertex3D(v[0], v[1], v[2]), Radius = 0 } )");
            sb.AppendLine("v = x,y,z => Fcs.Geometry.Vertex3D(x,y,z)");

            EntitiesToFcs(doc.Inserts);
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
                        case Mesh m:
                            DrawMesh(m);
                            break;
                        case LwPolyline lp:
                            DrawPolyline(lp);
                            break;
                        case Insert insert:
                            DrawInsert(insert);
                            break;
                        default:
                            Console.WriteLine($"Nepodporovaná entita: {item.GetType()}");
                            break;
                    }
                }
            }
        }

        private void SetNewTrans(Matrix3 tf, Vector3 tr)
        {
            var tra = transformations.Peek() * tf;
            var pos = translations.Peek() + transformations.Peek() * tr;
            transformations.Push(tra);
            translations.Push(pos);

            trans = tra;
            translation = pos;
        }

        private void BackTrans()
        {
            transformations.Pop();
            translations.Pop();

            trans = transformations.Peek();
            translation = translations.Peek();
        }

        private void DrawInsert(Insert insert)
        {
            var block = insert.Block;

            var drawingUnit = DrawingUnits.Millimeters;
            if (block.Owner is netDxf.Blocks.BlockRecord br)
                drawingUnit = br.Units;

            SetNewTrans(insert.GetTransformation(drawingUnit), insert.Position);

            EntitiesToFcs(block.Entities);

            BackTrans();
        }

        private void DrawPolyline(LwPolyline l)
        {
            var ex = l.Explode();
            EntitiesToFcs(ex);
        }

        private void DrawMesh(Mesh m)
        {
            foreach (var item in m.Faces)
            {
                DrawCurve(item.Select(vi => m.Vertexes[vi]).Append(m.Vertexes[item[0]]));
                sb.AppendLine($"area {{a{i}}} boundary curve {{c{i}}}");
                i++;
            }
        }

        private void DrawElipse(Ellipse e)
        {
            var elipseLines = e.ToPolyline(4).Explode();

            if (elipseLines != null && elipseLines[0] is Line la && elipseLines[2] is Line lb)
            {
                var A = la.StartPoint;
                var B = lb.StartPoint;
                var C = la.EndPoint;
                var D = lb.EndPoint;

                AppendLineVector3ToFcsVertex($"v{i}a", A);
                AppendLineVector3ToFcsVertex($"v{i}b", B);
                AppendLineVector3ToFcsVertex($"v{i}c", C);
                AppendLineVector3ToFcsVertex($"v{i}d", D);

                sb.AppendLine($"curve {{c{i}a}} arc vertex v{i}a v{i}c v{i}b ");
                sb.AppendLine($"curve {{c{i}b}} arc vertex v{i}b v{i}d v{i}a ");
            }
        }

        private void DrawCircle(Circle c)
        {
            var circleLines = c.ToPolyline(4).Explode();

            if (circleLines != null && circleLines[0] is Line la && circleLines[2] is Line lb)
            {
                var A = la.StartPoint;
                var B = lb.StartPoint;
                var C = la.EndPoint;
                var D = lb.EndPoint;

                AppendLineVector3ToFcsVertex($"v{i}a", A);
                AppendLineVector3ToFcsVertex($"v{i}b", B);
                AppendLineVector3ToFcsVertex($"v{i}c", C);
                AppendLineVector3ToFcsVertex($"v{i}d", D);

                sb.AppendLine($"curve {{c{i}a}} arc vertex v{i}a v{i}c v{i}b ");
                sb.AppendLine($"curve {{c{i}b}} arc vertex v{i}b v{i}d v{i}a ");
            }
        }

        private void DrawArc(Arc a)
        {
            var explodedEntities = a.ToPolyline(2).Explode();

            if (explodedEntities != null && explodedEntities[0] is Line la && explodedEntities[1] is Line lb)
            {
                var A = la.StartPoint;
                var B = lb.EndPoint;
                var C = lb.StartPoint;

                AppendLineVector3ToFcsVertex($"v{i}a", A);
                AppendLineVector3ToFcsVertex($"v{i}b", B);
                AppendLineVector3ToFcsVertex($"v{i}c", C);

                sb.AppendLine($"curve {{c{i}}} arc vertex v{i}a v{i}c v{i}b");
            }
        }

        private void DrawSpline(Spline l)
        {
            DrawCurve(l.ControlPoints.Select(p => p.Position));
        }

        private void DrawCurve(IEnumerable<Vector3> vectors)
        {
            sb.AppendLine($"vs{i} = [");
            foreach (var point in vectors)
            {
                AppendVector3ToFcsArray(point);
                sb.AppendLine(",");
            }
            sb.AppendLine($"]");

            sb.AppendLine($"curve {{c{i}}} filletedpoly items (cv(vs{i}))");
        }

        private void DrawLine(Line l)
        {
            AppendLineVector3ToFcsVertex($"v{i}a", l.StartPoint);
            AppendLineVector3ToFcsVertex($"v{i}b", l.EndPoint);

            sb.AppendLine($"curve {{c{i}}} vertex v{i}a v{i}b");
        }

        private void AppendLineVector3ToFcsVertex(string name, Vector3 vLocal)
        {
            var v = Transform(vLocal);

            if (oldVertex)
                sb.AppendLine($"vertex {{{name}}} xyz {Math.Round(v.X, precision)} {Math.Round(v.Y, precision)} {Math.Round(v.Z, precision)}");
            else
            {
                sb.Append($"{name}=v(");
                AppendNumber(v.X);
                sb.Append(",");
                AppendNumber(v.Y);
                sb.Append(",");
                AppendNumber(v.Z);
                sb.AppendLine(")");
            }
        }
        private void AppendVector3ToFcsArray(Vector3 vLocal)
        {
            var v = Transform(vLocal);

            sb.Append($"[");
            AppendNumber(v.X);
            sb.Append(",");
            AppendNumber(v.Y);
            sb.Append(",");
            AppendNumber(v.Z);
            sb.Append("]");
        }

        private void AppendNumber(double value)
        {
            sb.AppendFormat(numFormat, Math.Round(value, precision));
        }

        private Vector3 Transform(Vector3 point)
        {
            return (trans * point + translation) * unit;
        }
    }
}
