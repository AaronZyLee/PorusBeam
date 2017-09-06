using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace CreateBeamWithHoles
{

    public class unitRow
    {
        public int column;
        public Radius radius;

        public unitRow(int column, Radius radius)
        {
            this.column = column;
            this.radius = radius;
        }
    }

    public enum Radius
    {
        small,
        large,
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CreateBeamSection:IExternalCommand
    {
        const double LR_EDGE_S = 140;
        const double LR_EDGE_L = 160;
        const double INTV_S = 230;
        const double INTV_L = 260;
        const double D_S = 150;
        const double D_L = 175;
        const double TOP_S = 140;
        const double TOP_L = 160;
        const double BOTTOM_S = 180;
        const double BOTTOM_L = 190;
        const double INTV_H_S = 240;
        const double INTV_H_L = 260;

        Document doc;
        Document massdoc;
        Autodesk.Revit.Creation.FamilyItemFactory m_familyCreator;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            doc = app.ActiveUIDocument.Document;
            //massdoc = app.Application.NewFamilyDocument(@"C:\ProgramData\Autodesk\RVT 2016\Family Templates\Chinese\概念体量\公制结构框架 - 梁和支撑");
            //m_familyCreator = massdoc.FamilyCreate;

            //if (massdoc == null)
            //{
            //    TaskDialog.Show("error", "未找到梁的族文件");
            //    return Result.Failed;
            //}

            m_familyCreator = doc.FamilyCreate;
            Transaction trans0 = new Transaction(doc);
            trans0.Start("删除原模型");
            Element originEl = findElement(doc, typeof(Extrusion), "拉伸");
            if(originEl != null)
                doc.Delete(originEl.Id);
            trans0.Commit();

            Transaction trans = new Transaction(doc);
            trans.Start("创建梁截面");
            CreateBeam();
            //CreateIdenticalHolesBeam(2, 7, Radius.small);
            //List<unitRow> beamInfo = new List<unitRow>();
            //beamInfo.Add(new unitRow(5,Radius.large));
            //beamInfo.Add(new unitRow(6,Radius.small));
            //CreateDifferentSizeHolesBeam(beamInfo);
            trans.Commit();

 	        return Result.Succeeded;
        }

        public void CreateBeam(){
            Form1 form1 = new Form1();
            form1.ShowDialog();
            if (form1.isIdentical)
                CreateIdenticalHolesBeam(form1.row, form1.column, form1.radius);
            else
                CreateDifferentSizeHolesBeam(form1.beamInfo);

        }

        public void CreateIdenticalHolesBeam(int row, int column, Radius radius)
        {
            double width, depth,diameter;
            double interval, lr_edge, intv_h, top, bottom;
            if (radius == Radius.small)
            {
                interval = INTV_S;lr_edge = LR_EDGE_S;intv_h=INTV_S;top=TOP_S;bottom=BOTTOM_S;diameter = D_S;
            }
            else
            {
                interval = INTV_L;lr_edge = LR_EDGE_L;intv_h=INTV_L;top=TOP_L;bottom=BOTTOM_L;diameter = D_L;
            }

            width = interval*(column-1)+2*lr_edge;
            depth = intv_h*(row-1)+top+bottom;

            //ReferencePlane plane = findElement(doc, typeof(ReferencePlane), "中心(左/右)") as ReferencePlane;
            SketchPlane Splane = SketchPlane.Create(doc,new Plane(XYZ.BasisY,XYZ.BasisZ,XYZ.Zero));
            CurveArrArray caa = new CurveArrArray();

            XYZ p0 = new XYZ(0,mmToFeet(-width/2),0);
            XYZ p1 = new XYZ(0,mmToFeet(width/2),0);
            XYZ p2 = new XYZ(0,mmToFeet(width/2),mmToFeet(depth));
            XYZ p3 = new XYZ(0,mmToFeet(-width/2),mmToFeet(depth));

            Line l1 = Line.CreateBound(p0,p1);
            Line l2 = Line.CreateBound(p1,p2);
            Line l3 = Line.CreateBound(p2, p3);
            Line l4 = Line.CreateBound(p3, p0);

            CurveArray curveArr1 = new CurveArray();
            curveArr1.Append(l1);
            curveArr1.Append(l2);
            curveArr1.Append(l3);
            curveArr1.Append(l4);

            caa.Append(curveArr1);

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < column; j++) {
                    Arc circle = Arc.Create(new XYZ(0, mmToFeet(-width / 2 + lr_edge + interval * j), mmToFeet(bottom + intv_h * i)), 
                        mmToFeet(diameter / 2), 0, 2 * Math.PI, XYZ.BasisY, XYZ.BasisZ);
                    CurveArray circleCurve = new CurveArray();
                    circleCurve.Append(circle);
                    caa.Append(circleCurve);

                }
            }

            Extrusion beam = m_familyCreator.NewExtrusion(true, caa, Splane, mmToFeet(3000));
            ElementTransformUtils.MoveElement(doc, beam.Id, new XYZ(-mmToFeet(3000)/2,0,0));
            AddAlignment(beam, new XYZ(-1, 0, 0), "左");
            AddAlignment(beam, new XYZ(1, 0, 0), "右");

        }

        public void CreateDifferentSizeHolesBeam(List<unitRow> beamInfo ) {
            List<double> depthList = new List<double>();
            int maxWidthRowIndex;
            double width = 0, depth = 0;
            for (int i = 0; i < beamInfo.Count; i++)
            {
                double temp;
                if (beamInfo[i].radius == Radius.small)
                {
                    temp = INTV_S * (beamInfo[i].column - 1) + LR_EDGE_S * 2;
                    if (i == 0)
                        depth += TOP_S;
                    else if (i == beamInfo.Count - 1)
                        depth += (INTV_S + BOTTOM_S);
                    else
                        depth += INTV_S;
                }
                else
                {
                    temp = INTV_L * (beamInfo[i].column - 1) + LR_EDGE_L * 2;
                    if (i == 0)
                        depth += TOP_L;
                    else if (i == beamInfo.Count)
                    {
                        if (beamInfo[i - 1].radius == Radius.large)
                            depth += (INTV_L + BOTTOM_L);
                        else
                            depth += (INTV_S + BOTTOM_L);
                    }
                    else
                    {
                        if (beamInfo[i - 1].radius == Radius.large)
                            depth += INTV_L;
                        else
                            depth += INTV_S;
                    }
                }
                if (temp > width)
                {
                    width = temp;
                    maxWidthRowIndex = i;
                }
                depthList.Add(depth);
            }

            if (beamInfo[beamInfo.Count - 1].radius == Radius.small)
                depthList[depthList.Count - 1] -= BOTTOM_S;
            else
                depthList[depthList.Count - 1] -= BOTTOM_L;

            SketchPlane Splane = SketchPlane.Create(doc, new Plane(XYZ.BasisY, XYZ.BasisZ, XYZ.Zero));
            CurveArrArray caa = new CurveArrArray();

            XYZ p0 = new XYZ(0, mmToFeet(-width / 2), 0);
            XYZ p1 = new XYZ(0, mmToFeet(width / 2), 0);
            XYZ p2 = new XYZ(0, mmToFeet(width / 2), mmToFeet(depth));
            XYZ p3 = new XYZ(0, mmToFeet(-width / 2), mmToFeet(depth));

            Line l1 = Line.CreateBound(p0, p1);
            Line l2 = Line.CreateBound(p1, p2);
            Line l3 = Line.CreateBound(p2, p3);
            Line l4 = Line.CreateBound(p3, p0);

            CurveArray curveArr1 = new CurveArray();
            curveArr1.Append(l1);
            curveArr1.Append(l2);
            curveArr1.Append(l3);
            curveArr1.Append(l4);

            caa.Append(curveArr1);

            for (int i = 0; i < beamInfo.Count; i++)
            {
                double lr_edge,interval,diameter;
                if (beamInfo[i].radius == Radius.small)
                {
                    diameter = D_S;
                    interval = INTV_S;
                }
                else
                {
                    diameter = D_L;
                    interval = INTV_L;
                }
                lr_edge = (width - (beamInfo[i].column - 1) * interval) / 2;

                for (int j = 0; j < beamInfo[i].column; j++)
                {
                    Arc circle = Arc.Create(new XYZ(0, mmToFeet(-width / 2 + lr_edge + interval * j), mmToFeet(depth - depthList[i])),
                        mmToFeet(diameter / 2), 0, 2 * Math.PI, XYZ.BasisY, XYZ.BasisZ);
                    CurveArray circleCurve = new CurveArray();
                    circleCurve.Append(circle);
                    caa.Append(circleCurve);

                }
            }

            Extrusion beam = m_familyCreator.NewExtrusion(true, caa, Splane, mmToFeet(3000));
            ElementTransformUtils.MoveElement(doc, beam.Id, new XYZ(-mmToFeet(3000) / 2, 0, 0));

        }

        public void AddAlignment(Extrusion solid, XYZ normal, string nameRefPlane)
        {
            View pViewPlan = findElement(doc,typeof(View),"前") as View;
            ReferencePlane refPlane = findElement(doc,typeof(ReferencePlane),nameRefPlane) as ReferencePlane;
            PlanarFace pFace = findFace(solid,normal,refPlane);
            m_familyCreator.NewAlignment(pViewPlan,refPlane.GetReference(),pFace.Reference);
        }

        //根据类型及名称查找元素
        Element findElement(Document doc, Type targetType, string targetName)
        {
            // get the elements of the given type
            //
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.WherePasses(new ElementClassFilter(targetType));

            // parse the collection for the given name
            // using LINQ query here. 
            // 
            var targetElems = from element in collector where element.Name.Equals(targetName) select element;
            List<Element> elems = targetElems.ToList<Element>();

            if (elems.Count > 0)
            {  // we should have only one with the given name. 
                return elems[0];
            }

            // cannot find it.
            return null;
        }

        //毫米英尺单位转化
        double mmToFeet(double mmVal)
        {
            return mmVal / 304.8;
        }

        PlanarFace findFace(Extrusion pBox, XYZ normal, ReferencePlane refPlane)
        {
            // get the geometry object of the given element
            //
            Options op = new Options();
            op.ComputeReferences = true;
            GeometryElement geomObjs = pBox.get_Geometry(op);

            // loop through the array and find a face with the given normal
            //
            foreach (GeometryObject geomObj in geomObjs)
            {
                if (geomObj is Solid)  // solid is what we are interested in.
                {
                    Solid pSolid = geomObj as Solid;
                    FaceArray faces = pSolid.Faces;
                    PlanarFace pPlanarFace = null;
                    foreach (Face pFace in faces)
                    {
                        if (pFace is PlanarFace)
                        {
                            pPlanarFace = (PlanarFace)pFace;
                            // check to see if they have same normal
                            if ((pPlanarFace != null) && pPlanarFace is PlanarFace && pPlanarFace.FaceNormal.IsAlmostEqualTo(normal))
                            {
                                // additionally, we want to check if the face is on the reference plane
                                //
                                XYZ p0 = refPlane.BubbleEnd;
                                XYZ p1 = refPlane.FreeEnd;
                                Line pCurve = Line.CreateBound(p0, p1);
                                if (pPlanarFace.Intersect(pCurve) == SetComparisonResult.Subset)
                                {
                                    return pPlanarFace; // we found the face
                                }
                            }
                        }

                    }
                }
            }
            // if we come here, we did not find any.
            return null;
        }
    }
}
