using netDxf;
using netDxf.Entities;
using netDxf.Units;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dxf2fcs
{
    public struct Vector3Radius
    {
        public Vector3 Point;
        public double Radius;

        public Vector3Radius(Vector3 point, double radius = 0)
        {
            Point = point;
            Radius = radius;
        }
    }

    public class DxfLoader
    {
        private readonly double unit;
        private readonly int precision;
        private readonly string numFormat;
        private readonly bool oldVertex = false;
        private readonly StringBuilder sb;
        private readonly Stack<Matrix3> transformations;
        private readonly Stack<Vector3> translations;

        private int iDrawed;
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
            iDrawed = 0;

            trans = Matrix3.Identity;
            translation = Vector3.Zero;

            transformations.Clear();
            transformations.Push(trans);
            translations.Clear();
            translations.Push(translation);

            sb.AppendLine("cv = ar => ar.Select(v => { Vertex = Fcs.Geometry.Vertex3D(v[0], v[1], v[2]), Radius = 0 } )");
            sb.AppendLine("cvr = ar => ar.Select(v => { Vertex = Fcs.Geometry.Vertex3D(v[0], v[1], v[2]), Radius = v[3] } )");
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
            EntitiesToFcs(doc.Hatches);
            //EntitiesToFcs(doc.Groups);
            return sb.ToString();
        }

        private void EntitiesToFcs(IEnumerable<EntityObject> entities)
        {
            {
                foreach (var item in entities)
                {
                    iDrawed++;
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
                        case Hatch hatch:
                            DrawHatch(hatch);
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

        private void DrawPolyline2(LwPolyline l)
        {
            var v = new List<Vector3>();

            foreach (var e in l.Vertexes)
            {
                var p1 = MathHelper.Transform(new Vector3(e.Position.X, e.Position.Y, l.Elevation), l.Normal, CoordinateSystem.Object, CoordinateSystem.World);

                v.Add(p1);
            }

            if (l.IsClosed)
            {
                v.Add(v[0]);
            }

            DrawCurve(v);
        }

        private void DrawPolyline(LwPolyline l)
        {
            var ex = l.Explode();

            var v = new List<Vector3>();

            var i = 0;
            foreach (var en in ex)
            {
                if (en is Line line)
                {
                    var A = line.StartPoint;
                    var B = line.EndPoint;

                    if (i == 0)
                    {
                        v.Add((A));
                        v.Add((B));
                    }
                    else {
                        var L = v[v.Count - 1];

                        // if the first entity was in wrong orientation
                        if (i == 1 && !L.Equals(A, Math.Pow(10, -8)) && !L.Equals(B, Math.Pow(10, -8)))
                        {
                            v.Reverse();
                        }

                        // if current entity is in wrong orientation
                        if (L.Equals(A, Math.Pow(10, -8)))
                        {
                            v.Add((B));
                        }
                        else
                        {
                            v.Add((A));
                            v.Add((B));
                        }
                    }
                }

                else if (en is Arc arc)
                {
                    //AddArcToPolylineNew(arc, i, v);
                    AddArcToPolyline(arc, i, v);
                }

                i++;
            }

            if (l.IsClosed)
            {
                v.Add(v[0]);
            }

            DrawCurve(v);
        }

        private void AddArcToPolylineNew(Arc arc, int i, List<Vector3Radius> v)
        {
            var linesAB = arc.ToPolyline(3).Explode().Select(e => (Line)e).ToArray();

            var A = linesAB[0].StartPoint;
            var B = linesAB[2].EndPoint;
            var S = (A + B) / 2;
            var C = arc.Center;

            var r = arc.Radius;

            var U = S - C;
            U.Normalize();

            var phi = Math.Abs(arc.StartAngle - arc.EndAngle) / 2;
            var d = r / Math.Cos(phi / 180 * Math.PI);

            var X = C + d * U;

            if (i == 0)
            {
                v.Add(new Vector3Radius(A));
                v.Add(new Vector3Radius(X, r));
                v.Add(new Vector3Radius(B));
            }
            else
            {
                var L = v[v.Count - 1].Point;

                // if the first entity was in wrong orientation
                if (i == 1 && !L.Equals(A, Math.Pow(10, -8)) && !L.Equals(B, Math.Pow(10, -8)))
                {
                    v.Reverse();
                }

                // if current entity is in wrong orientation
                if (!L.Equals(A, Math.Pow(10, -8)))
                {
                    v.Add(new Vector3Radius(B));
                    v.Add(new Vector3Radius(X));
                    v.Add(new Vector3Radius(A));
                }
                else
                {
                    v.Add(new Vector3Radius(A));
                    v.Add(new Vector3Radius(X));
                    v.Add(new Vector3Radius(B));
                }
            }
        }

        private void AddArcToPolyline(Arc arc, int i, List<Vector3> v)
        {
            var angle = Math.Abs((arc.EndAngle < arc.StartAngle ? arc.EndAngle + 360 : arc.EndAngle) - arc.StartAngle);
            if (angle == 0) angle = 360;

            var poly = arc.ToPolyline(Math.Max(3, (int)(angle / 90 * 8))).Explode().Select(l => (Line)l).ToList();

            var A = poly[0].StartPoint;
            var B = poly[poly.Count - 1].EndPoint;

            var reversed = false;

            if (i == 0)
            {
                v.Add(A);
            }
            else
            {
                var L = v[v.Count - 1];

                // if the first entity was in wrong orientation
                if (i == 1 && !L.Equals(A, Math.Pow(10, -8)) && !L.Equals(B, Math.Pow(10, -8)))
                {
                    v.Reverse();
                }

                // if current entity is in wrong orientation
                if (!L.Equals(A, Math.Pow(10, -8)))
                {
                    poly.Reverse();
                    reversed = true;
                }
            }

            foreach (var arcLine in poly)
            {
                v.Add(reversed ? arcLine.StartPoint : arcLine.EndPoint);
            }
        }

        private void DrawPolyline(Polyline l)
        {
            DrawCurve(l.Vertexes.Select(v => v.Position));
        }

        private void DrawMesh(Mesh m)
        {
            foreach (var item in m.Faces)
            {
                DrawCurve(item.Select(vi => m.Vertexes[vi]).Append(m.Vertexes[item[0]]));
                sb.AppendLine($"area {{a{iDrawed}}} boundary curve {{c{iDrawed}}}");
                iDrawed++;
            }
        }

        private void DrawElipse(Ellipse e)
        {
            var angle = Math.Abs(e.EndAngle - e.StartAngle);
            if (angle == 0) angle = 360;
            var poly = e.ToPolyline((int)(angle / 90 * 12));
            DrawPolyline(poly);
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

                AppendLineVector3ToFcsVertex($"v{iDrawed}a", A);
                AppendLineVector3ToFcsVertex($"v{iDrawed}b", B);
                AppendLineVector3ToFcsVertex($"v{iDrawed}c", C);
                AppendLineVector3ToFcsVertex($"v{iDrawed}d", D);

                sb.AppendLine($"curve {{c{iDrawed}a}} arc vertex v{iDrawed}a v{iDrawed}c v{iDrawed}b ");
                sb.AppendLine($"curve {{c{iDrawed}b}} arc vertex v{iDrawed}b v{iDrawed}d v{iDrawed}a ");
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

                AppendLineVector3ToFcsVertex($"v{iDrawed}a", A);
                AppendLineVector3ToFcsVertex($"v{iDrawed}b", B);
                AppendLineVector3ToFcsVertex($"v{iDrawed}c", C);

                sb.AppendLine($"curve {{c{iDrawed}}} arc vertex v{iDrawed}a v{iDrawed}c v{iDrawed}b");
            } 
        }

        private void DrawSpline(Spline l)
        {
            DrawCurve(l.PolygonalVertexes(l.ControlPoints.Count * 2));
          //  DrawPolyline(l.ToPolyline(l.ControlPoints.Count * 1));
        }

        private void DrawHatch(Hatch h)
        {
            var sbArea = new StringBuilder();
            foreach (var bPath in h.BoundaryPaths)
            {
                var isBoundaryCurve = !bPath.PathType.HasFlag(HatchBoundaryPathTypeFlags.Outermost);
                var isOpeningCurve = bPath.PathType.HasFlag(HatchBoundaryPathTypeFlags.Outermost);

                var startI = iDrawed + 1;
                var b = bPath.Edges.Select(e => e.ConvertTo()).ToArray();

                foreach (var path in b)
                {
                    EntitiesToFcs(new[] { path });
                }

                if (isBoundaryCurve)
                {
                    // every boundary curve create new area
                    if (sbArea.Length > 0)
                    {
                        sb.AppendLine(sbArea.ToString());
                        sbArea.Clear();
                    }

                    sbArea.Append($"area {{a{startI}}} boundary curve");
                }
                else if (isOpeningCurve)
                {
                    sbArea.Append($" opening curve");
                }

                for (int i = startI; i <= iDrawed; i++)
                {
                    sbArea.Append($" +{{c{i}}}");
                }
            }

            sb.AppendLine(sbArea.ToString());
        }

        private void DrawCurve(IEnumerable<Vector3> vectors)
        {
            sb.AppendLine($"vs{iDrawed} = [");
            foreach (var point in vectors)
            {
                AppendVector3ToFcsArray(point);
                sb.AppendLine(",");
            }
            sb.AppendLine($"]");

            sb.AppendLine($"curve {{c{iDrawed}}} filletedpoly items (cv(vs{iDrawed}))");
        }

        private void DrawCurve(IEnumerable<Vector3Radius> vectors)
        {
            sb.AppendLine($"vs{iDrawed} = [");
            foreach (var point in vectors)
            {
                AppendVector3ToFcsArray(point);
                sb.AppendLine(",");
            }
            sb.AppendLine($"]");

            sb.AppendLine($"curve {{c{iDrawed}}} filletedpoly items (cvr(vs{iDrawed})) radius 1");
        }

        private void DrawLine(Line l)
        {
            AppendLineVector3ToFcsVertex($"v{iDrawed}a", l.StartPoint);
            AppendLineVector3ToFcsVertex($"v{iDrawed}b", l.EndPoint);

            sb.AppendLine($"curve {{c{iDrawed}}} vertex v{iDrawed}a v{iDrawed}b");
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

        private void AppendVector3ToFcsArray(Vector3Radius vLocal)
        {
            var v = Transform(vLocal.Point);

            sb.Append($"[");
            AppendNumber(v.X);
            sb.Append(",");
            AppendNumber(v.Y);
            sb.Append(",");
            AppendNumber(v.Z);
            sb.Append(",");
            AppendNumber(vLocal.Radius);
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
