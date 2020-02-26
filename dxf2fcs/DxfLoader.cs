﻿using netDxf;
using netDxf.Entities;
using netDxf.Units;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dxf2fcs
{
    public class Vector3Radius
    {
        public Vector3 Point;
        public double Radius;

        public Vector3Radius(Vector3 point, double radius = 0)
        {
            Point = point;
            Radius = radius;
        }
    }

    public class Vector3Middle
    {
        public Vector3 Point;
        public Vector3 MiddlePoint;

        public Vector3Middle(Vector3 point, Vector3 middlePoint = default)
        {
            Point = point;
            MiddlePoint = middlePoint;
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

        //private int iDrawed;

        private int iDrawedLine;
        private int iDrawedArea;

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
            iDrawedLine = 0;
            iDrawedArea = 0;

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

        private List<int> EntitiesToFcs(IEnumerable<EntityObject> entities)
        {
            var ids = new List<int>();

            foreach (var item in entities)
            {
                switch (item)
                {
                    case Line l:
                        AddToIds(ids, DrawLine(l));
                        break;
                    case Spline l:
                        AddToIds(ids, DrawSpline(l));
                        break;
                    case Arc a:
                        AddToIds(ids, DrawArc(a));
                        break;
                    case Circle c:
                        AddToIds(ids, DrawCircle(c));
                        break;
                    case Ellipse e:
                        AddToIds(ids, DrawElipse(e));
                        break;
                    case Mesh m:
                        DrawMesh(m);
                        break;
                    case LwPolyline lp:
                        AddToIds(ids, DrawPolyline(lp));
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

            return ids;
        }

        private void AddToIds(List<int> ids, List<int> nextIds)
        {
            if (nextIds != null && nextIds.Count >= 0)
            {
                ids.AddRange(nextIds);
            }
        }

        private void AddToIds(List<int> ids, int nextId)
        {
            if (nextId >= 0)
            {
                ids.Add(nextId);
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

        private List<int> DrawPolyline(LwPolyline l)
        {
            var ex = l.Explode();

            var v = new List<Vector3Middle>();

            var i = 0;
            foreach (var en in ex)
            {
                if (en is Line line)
                {
                    var A = line.StartPoint;
                    var B = line.EndPoint;

                    if (i == 0)
                    {
                        v.Add(new Vector3Middle(A));
                        v.Add(new Vector3Middle(B));
                    }
                    else {
                        var L = v[v.Count - 1].Point;

                        // if the first entity was in wrong orientation
                        if (i == 1 && !L.Equals(A, Math.Pow(10, -8)) && !L.Equals(B, Math.Pow(10, -8)))
                        {
                            ReverseFcsPolyline(v);
                        }

                        // if current entity is in wrong orientation
                        if (L.Equals(A, Math.Pow(10, -8)))
                        {
                            v.Add(new Vector3Middle(B));
                        }
                        else
                        {
                            v.Add(new Vector3Middle(A));
                 //           v.Add(new Vector3Radius(B));
                        }
                    }
                }

                else if (en is Arc arc)
                {
                    //AddArcToPolylineNew(arc, i, v);
                    AddArcToArc(arc, i, v);
                }

                i++;
            }

            var Lc = v.Last().Point;
            if (l.IsClosed && !Lc.Equals(v[0].Point, Math.Pow(10, -8)))
            {
                v.Add(v[0]);
            }


            var ids = new List<int>();

            // draw
            var polyline = new List<Vector3>();
            polyline.Add(v[0].Point);

            for (int j = 1; j < v.Count; j++)
            {
                var cr = v[j];

                if (cr.MiddlePoint == default)
                {
                    polyline.Add(cr.Point);
                }
                else
                {
                    if (polyline.Count > 1)
                    {
                        ids.Add(DrawCurve(polyline));
                    }

                    var A = polyline.Last();
                    var B = cr.Point;
                    var C = cr.MiddlePoint;

                    ids.Add(DrawArc(A, B, C));
                    
                    polyline.Clear();
                    polyline.Add(B);
                }
            }

            if (polyline.Count > 1)
            {
                ids.Add(DrawCurve(polyline));
            }

            return ids;
        }

        private void ReverseFcsPolyline(List<Vector3Middle> v)
        {
            v.Reverse();
            for (int i = 1; i < v.Count; i++)
            {
                v[i].MiddlePoint = v[i - 1].MiddlePoint;
            }
            v[0].MiddlePoint = default;
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

        private void AddArcToArc(Arc arc, int i, List<Vector3Middle> v)
        {
            var poly = arc.ToPolyline(2).Explode().Select(l => (Line)l).ToList();

            if (!(poly != null && poly[0] is Line la && poly[1] is Line lb))
            {
                return;
            }

            var A = la.StartPoint;
            var B = lb.EndPoint;
            var C = lb.StartPoint;

            if (i == 0)
            {
                v.Add(new Vector3Middle(A));
                v.Add(new Vector3Middle(B, C));
            }
            else
            {
                var L = v[v.Count - 1].Point;

                // if the first entity was in wrong orientation
                if (i == 1 && !L.Equals(A, Math.Pow(10, -8)) && !L.Equals(B, Math.Pow(10, -8)))
                {
                    ReverseFcsPolyline(v);
                }

                // if current entity is in wrong orientation
                if (L.Equals(A, Math.Pow(10, -8)))
                {
                    v.Add(new Vector3Middle(B, C));
                }
                else
                {
                    v.Add(new Vector3Middle(A, C));
                  //  v.Add(new Vector3Radius(B));
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
                var ic = DrawCurve(item.Select(vi => m.Vertexes[vi]).Append(m.Vertexes[item[0]]));
                var ia = ++iDrawedArea;
                sb.AppendLine($"area {{a{ia}}} boundary curve {{c{ic}}} mapping Linear");
            }
        }

        private List<int> DrawElipse(Ellipse e)
        {
            var angle = Math.Abs(e.EndAngle - e.StartAngle);
            if (angle == 0) angle = 360;
            var poly = e.ToPolyline((int)(angle / 90 * 12));
            return DrawPolyline(poly);
        }

        private List<int> DrawCircle(Circle c)
        {
            var circleLines = c.ToPolyline(4).Explode();

            if (circleLines != null && circleLines[0] is Line la && circleLines[2] is Line lb)
            {
                var i1 = ++iDrawedLine;
                var i2 = ++iDrawedLine;

                var A = la.StartPoint;
                var B = lb.StartPoint;
                var C = la.EndPoint;
                var D = lb.EndPoint;

                AppendLineVector3ToFcsVertex($"v{i1}a", A);
                AppendLineVector3ToFcsVertex($"v{i1}b", B);
                AppendLineVector3ToFcsVertex($"v{i1}c", C);
                AppendLineVector3ToFcsVertex($"v{i1}d", D);


                sb.AppendLine($"curve {{c{i1}}} arc vertex v{i1}a v{i1}c v{i1}b ");
                sb.AppendLine($"curve {{c{i2}}} arc vertex v{i1}b v{i1}d v{i1}a ");

                return new List<int> { i1, i2 };
            }

            return default;
        }

        private int DrawArc(Arc a)
        {
            var explodedEntities = a.ToPolyline(2).Explode();
            
            if (explodedEntities != null && explodedEntities[0] is Line la && explodedEntities[1] is Line lb)
            {
                var A = la.StartPoint;
                var B = lb.EndPoint;
                var C = lb.StartPoint;

                return DrawArc(A, B, C);
            }

            return -1;
        }

        private int DrawArc(Vector3 A, Vector3 B, Vector3 C)
        {
            var i = ++iDrawedLine;

            AppendLineVector3ToFcsVertex($"v{i}a", A);
            AppendLineVector3ToFcsVertex($"v{i}b", B);
            AppendLineVector3ToFcsVertex($"v{i}c", C);

            sb.AppendLine($"curve {{c{i}}} arc vertex v{i}a v{i}c v{i}b");

            return i;
        }

        private int DrawSpline(Spline l)
        {
            return DrawCurve(l.PolygonalVertexes(l.ControlPoints.Count * 2));
        }

        private void DrawHatch(Hatch h)
        {
            var sbArea = new StringBuilder();
            foreach (var bPath in h.BoundaryPaths)
            {
                var isBoundaryCurve = !bPath.PathType.HasFlag(HatchBoundaryPathTypeFlags.Outermost);
                var isOpeningCurve = bPath.PathType.HasFlag(HatchBoundaryPathTypeFlags.Outermost);

                //var startI = iDrawed + 1;
                var b = bPath.Edges.Select(e => e.ConvertTo()).ToArray();

                var ids = new List<int>();

                foreach (var path in b)
                {
                    AddToIds(ids, EntitiesToFcs(new[] { path }));
                }

                if (isBoundaryCurve)
                {
                    // every boundary curve create new area
                    if (sbArea.Length > 0)
                    {
                        sb.AppendLine(sbArea.ToString());
                        sbArea.Clear();
                    }

                    var i = ++iDrawedArea;
                    sbArea.Append($"area {{a{i}}} boundary curve");
                }
                else if (isOpeningCurve)
                {
                    sbArea.Append($" opening curve");
                }

                foreach (var i in ids)
                {
                    sbArea.Append($" +{{c{i}}}");
                }

                if (ids.Count == 4)
                {
                    sbArea.Append(" mapping Linear");
                }
            }

            sb.AppendLine(sbArea.ToString());
        }


        private int DrawCurve(IEnumerable<Vector3> vectors)
        {
            var i = ++iDrawedLine;

            sb.AppendLine($"vs{i} = [");
            foreach (var point in vectors)
            {
                AppendVector3ToFcsArray(point);
                sb.AppendLine(",");
            }
            sb.AppendLine($"]");

            sb.AppendLine($"curve {{c{i}}} filletedpoly items (cv(vs{i}))");

            return i;
        }

        private int DrawLine(Line l)
        {
            var i = ++iDrawedLine;

            AppendLineVector3ToFcsVertex($"v{i}a", l.StartPoint);
            AppendLineVector3ToFcsVertex($"v{i}b", l.EndPoint);

            sb.AppendLine($"curve {{c{i}}} vertex v{i}a v{i}b");

            return i;
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
