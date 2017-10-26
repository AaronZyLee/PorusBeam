using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using System.Text.RegularExpressions;

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

        /// <summary>
        /// 创建相同孔径的梁截面
        /// </summary>
        /// <param name="row">孔的排数</param>
        /// <param name="column">孔的列数</param>
        /// <param name="radius">孔径大小</param>
        public void CreateIdenticalHolesBeam(int row, int column, Radius radius)
        {
            //计算梁的长度及宽度
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

            SketchPlane Splane = SketchPlane.Create(doc,new Plane(XYZ.BasisY,XYZ.BasisZ,XYZ.Zero));
            CurveArrArray caa = new CurveArrArray();

            //绘制梁的外轮廓
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

            //绘制孔的轮廓
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

            //生成梁拉伸体，并移动至中心位置
            Extrusion beam = m_familyCreator.NewExtrusion(true, caa, Splane, mmToFeet(3000));
            ElementTransformUtils.MoveElement(doc, beam.Id, new XYZ(-mmToFeet(3000)/2,0,0));

            //将梁边界与参照边界对齐
            AddAlignment(beam, new XYZ(-1, 0, 0), "左");
            AddAlignment(beam, new XYZ(1, 0, 0), "右");

        }

        /// <summary>
        /// 绘制多孔径梁
        /// </summary>
        /// <param name="beamInfo">储存每排孔径信息的集合，顺序从上到下</param>
        public void CreateDifferentSizeHolesBeam(List<unitRow> beamInfo ) {
            List<double> depthList = new List<double>();
            int maxWidthRowIndex;
            double width = 0, depth = 0;

            //计算梁截面的长度、宽度
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

            //修正depthList最后一个值
            if (beamInfo[beamInfo.Count - 1].radius == Radius.small)
                depthList[depthList.Count - 1] -= BOTTOM_S;
            else
                depthList[depthList.Count - 1] -= BOTTOM_L;

            SketchPlane Splane = SketchPlane.Create(doc, new Plane(XYZ.BasisY, XYZ.BasisZ, XYZ.Zero));
            CurveArrArray caa = new CurveArrArray();

            //绘制梁的外轮廓
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

            //绘制各孔径轮廓
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

            //生成梁拉伸体
            Extrusion beam = m_familyCreator.NewExtrusion(true, caa, Splane, mmToFeet(3000));
            ElementTransformUtils.MoveElement(doc, beam.Id, new XYZ(-mmToFeet(3000) / 2, 0, 0));
            
            //将梁边界与参照边界对齐
            AddAlignment(beam, new XYZ(-1, 0, 0), "左");
            AddAlignment(beam, new XYZ(1, 0, 0), "右");
        }

        #region Helper Functions

        //添加对齐
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

        //在拉伸体上找到相应的面
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

        #endregion
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class AssemblePoleSection : IExternalCommand
    {
        Autodesk.Revit.Creation.FamilyItemFactory m_familyCreator;
        Document doc;
        FileStream dataFile;
        StreamReader sr;
        String fileName1, fileName2, fileName3;
        Dictionary<int, XYZ> pointInfo;
        Dictionary<int, IList<double>> elementInfo;


        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            doc = app.ActiveUIDocument.Document;
            //m_familyCreator = doc.FamilyCreate;
            Form2 form2 = new Form2();
            if (form2.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                fileName1 = form2.fileName1;
                fileName2 = form2.fileName2;
                fileName3 = form2.fileName3;
                if (insertDataset()) {
                    dataFile = new FileStream(fileName2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    sr = new StreamReader(dataFile, System.Text.Encoding.GetEncoding(936));
                    //insertDataset();
                    //FamilySymbol fs = getSymbolType(doc, "电塔杆组件");
                    //FamilySymbol fs = getSymbolType(doc, "族1");
                    FamilySymbol fs = getSymbolType(doc, "电塔杆单元");

                    if (fs != null)
                    {
                        TaskDialog.Show("1", "ok!");
                        Transaction trans = new Transaction(doc);
                        trans.Start("创建电塔杆");
                        fs.Activate();
                        CreateColumn1(fs);
                        trans.Commit();
                        return Result.Succeeded;

                    }
                    else
                        return Result.Failed;
                }         
            }
            return Result.Failed;
            //throw new NotImplementedException();
        }

        /// <summary>
        /// 初始化数据
        /// </summary>
        /// <returns></returns>
        public bool insertDataset()
        {
            //Form2 form2 = new Form2();
            //if (form2.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            if(fileName1!=null&&fileName3!=null)
            {   
                //导入点的信息
                FileStream dataFile1 = new FileStream(fileName1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                StreamReader sr1 = new StreamReader(dataFile1, System.Text.Encoding.GetEncoding(936));
                pointInfo = new Dictionary<int, XYZ>();
                string str = sr1.ReadLine();
                while (str != null)
                {
                    str = sr1.ReadLine();
                    if (str == null)
                        break;
                    //string[] data = str.Split(',');
                    string[] data = Regex.Split(str, @"\s+");
                    pointInfo.Add(int.Parse(data[1]), new XYZ(mToFeet(double.Parse(data[2])), mToFeet(double.Parse(data[3])), -mToFeet(double.Parse(data[4]))));
                }
                sr1.Close();

                //导入杆件信息
                FileStream dataFile2 = new FileStream(fileName3, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                StreamReader sr2 = new StreamReader(dataFile2, System.Text.Encoding.GetEncoding(936));
                elementInfo = new Dictionary<int,IList<double>>();
                str = sr2.ReadLine();
                while (str != null)
                {
                    str = sr2.ReadLine();
                    if (str == null)
                        break;
                    //string[] data = str.Split(',');
                    string[] data = Regex.Split(str, @"\s+");
                    IList<double> paramInfo = new List<double>();
                    paramInfo.Add(double.Parse(data[2]));paramInfo.Add(double.Parse(data[3]));paramInfo.Add(double.Parse(data[4]));
                    elementInfo.Add(int.Parse(data[1]), paramInfo);
                }
                sr2.Close();

                return true;
            }
            return false;
        }

        public void CreateColumn1(FamilySymbol fs)
        {
            Level level = findElement(doc, typeof(Level), "标高 1") as Level;
            if (level == null)
            {
                TaskDialog.Show("1", "error");
                return;
            }

            string str = sr.ReadLine();
            while (str != null)
            {
                str = sr.ReadLine();
                if (str == null)
                    break;
                string[] data = Regex.Split(str, @"\s+");
                int startpoint = int.Parse(data[2]);
                int endpoint = int.Parse(data[3]);
                int param = int.Parse(data[4]);
                Curve line = Line.CreateBound(pointInfo[startpoint], pointInfo[endpoint]);
                FamilyInstance column = doc.Create.NewFamilyInstance(line, fs, level, Autodesk.Revit.DB.Structure.StructuralType.Beam);
                column.get_Parameter(BuiltInParameter.Z_JUSTIFICATION).Set(2);
                column.LookupParameter("顶面直径").Set(mmToFeet(elementInfo[param][0]));
                column.LookupParameter("底面直径").Set(mmToFeet(elementInfo[param][1]));
                column.LookupParameter("壁厚").Set(mmToFeet(elementInfo[param][2]));
            }
            //Curve line = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(0, 0, 1000));

            //if (fi == null)
            //    TaskDialog.Show("1", "failed");

        }

        public void CreateColumn2(FamilySymbol fs)
        {

        }


        public void CreateColumn(FamilySymbol fs)
        {
            string str = sr.ReadLine();
            while (str != null)
            {
                str = sr.ReadLine();
                if (str == null)
                    break;
                string[] data = str.Split(',');
                FamilyInstance column = m_familyCreator.NewFamilyInstance(new XYZ(0,0,mmToFeet(Double.Parse(data[3])*1000)),fs,Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                column.LookupParameter("顶面直径").Set(mmToFeet(Double.Parse(data[0])));
                column.LookupParameter("底面直径").Set(mmToFeet(Double.Parse(data[1])));
                column.LookupParameter("壁厚").Set(mmToFeet(Double.Parse(data[2])));
                column.LookupParameter("高度").Set(mmToFeet(Double.Parse(data[4])*1000));
            } 
            sr.Close();
        }

        /// <summary>
        /// 根据名称查找familysymbol
        /// </summary>
        /// <param name="doc">项目文件</param>
        /// <param name="name">symbol名称</param>
        /// <returns>familysymbol</returns>
        public FamilySymbol getSymbolType(Document doc, string name)
        {
            FilteredElementIdIterator workWellItrator = new FilteredElementCollector(doc).OfClass(typeof(Family)).GetElementIdIterator();
            workWellItrator.Reset();
            FamilySymbol getsymbol = null;
            while (workWellItrator.MoveNext())
            {
                Family family = doc.GetElement(workWellItrator.Current) as Family;
                foreach (ElementId id in family.GetFamilySymbolIds())
                {
                    FamilySymbol symbol = doc.GetElement(id) as FamilySymbol;
                    if (symbol.Name == name)
                    {
                        getsymbol = symbol;
                    }
                }
            }
            return getsymbol;

        }

        //毫米英尺单位转化
        double mmToFeet(double mmVal)
        {
            return mmVal / 304.8;
        }

        /// <summary>
        /// 米英尺单位转化
        /// </summary>
        /// <param name="mVal"></param>
        /// <returns></returns>
        double mToFeet(double mVal)
        {
            return mVal * 1000 / 304.8;
        }

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
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class AssemblePoleSectionInFamily : IExternalCommand
    {
        Autodesk.Revit.Creation.FamilyItemFactory m_familyCreator;
        Document doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            doc = app.ActiveUIDocument.Document;
            m_familyCreator = doc.FamilyCreate;            
            //FamilySymbol fs = getSymbolType(doc, "电塔杆单元");
            //if (fs != null)
            //{
            //    TaskDialog.Show("1", "ok!");
            //    Transaction trans = new Transaction(doc);
            //    trans.Start("创建电塔杆");
            //    fs.Activate();
            //    Line pl = Line.CreateBound(XYZ.Zero, new XYZ(0, 0, 100));
            //    m_familyCreator.NewFamilyInstance(pl, fs, view); 
            //    //CreateColumn1(fs);
            //    trans.Commit();
            //    return Result.Succeeded;
            //}

            Plane p = new Plane(new XYZ(3,4,5),new XYZ(1,2,3));

            //Transform transform = Transform.CreateReflection(p);
            //transform.BasisX = p.XVec;
            //transform.BasisY = p.YVec;
            //transform.BasisZ = p.Normal;
            //transform.Origin = p.Origin;
            Transaction trans = new Transaction(doc);
            trans.Start("创建拉伸");

            //CreatePoleSection(transform,p);
            //CreateLShapeSection(p, 400, 40);
            //CreateLoopSection(p, 400, 300);
            CreateNPolygonSection(p, 400, 16);
            trans.Commit();
            return Result.Succeeded;
        }

        public void CreatePoleSection(Transform transform, Plane p) 
        {

            SketchPlane Splane = SketchPlane.Create(doc, p);
            CurveArrArray caa = new CurveArrArray();

            double height = mmToFeet(200);
            double width = mmToFeet(400);
            XYZ p0 = transform.OfPoint(new XYZ(-width / 2, height / 2, 0));
            XYZ p1 = transform.OfPoint(new XYZ(width / 2, height / 2, 0));
            XYZ p2 = transform.OfPoint(new XYZ(width / 2, -height / 2, 0));
            XYZ p3 = transform.OfPoint(new XYZ(-width / 2, -height / 2, 0));

            Line l1 = Line.CreateBound(p0, p1);
            Line l2 = Line.CreateBound(p1, p2);
            Line l3 = Line.CreateBound(p2, p3);
            Line l4 = Line.CreateBound(p3, p0);

            //ModelCurve cl1 = m_familyCreator.NewModelCurve(l1, Splane);

            CurveArray curveArr1 = new CurveArray();
            curveArr1.Append(l1);
            curveArr1.Append(l2);
            curveArr1.Append(l3);
            curveArr1.Append(l4);

            caa.Append(curveArr1);

            Extrusion beam = m_familyCreator.NewExtrusion(true, caa, Splane, mmToFeet(3000));
        }

        /// <summary>
        /// 创建等边L型截面
        /// </summary>
        /// <param name="p">创建截面所在的平面</param>
        /// <param name="width">L型截面边长</param>
        /// <param name="thick">厚度</param>
        /// <returns></returns>
        public CurveArrArray CreateLShapeSection(Plane p, double width, double thick)
        {
            CurveArrArray caa = new CurveArrArray();
            width = mmToFeet(width);
            thick = mmToFeet(thick);

            Transform transform = Transform.CreateReflection(p);
            transform.BasisX = p.XVec;
            transform.BasisY = p.YVec;
            transform.BasisZ = p.Normal;
            transform.Origin = p.Origin;

            XYZ p0 = transform.OfPoint(XYZ.Zero);
            XYZ p1 = transform.OfPoint(new XYZ(0,width,0));
            XYZ p2 = transform.OfPoint(new XYZ(thick,width,0));
            XYZ p3 = transform.OfPoint(new XYZ(thick,thick,0));
            XYZ p4 = transform.OfPoint(new XYZ(width, thick, 0));
            XYZ p5 = transform.OfPoint(new XYZ(width, 0, 0));

            Line l1 = Line.CreateBound(p0, p1);
            Line l2 = Line.CreateBound(p1, p2);
            Line l3 = Line.CreateBound(p2, p3);
            Line l4 = Line.CreateBound(p3, p4);
            Line l5 = Line.CreateBound(p4, p5);
            Line l6 = Line.CreateBound(p5, p0);

            CurveArray curveArr1 = new CurveArray();
            curveArr1.Append(l1);
            curveArr1.Append(l2);
            curveArr1.Append(l3);
            curveArr1.Append(l4);
            curveArr1.Append(l5);
            curveArr1.Append(l6);

            m_familyCreator.NewModelCurveArray(curveArr1, SketchPlane.Create(doc, p));

            caa.Append(curveArr1);

            return caa;
        } 

        /// <summary>
        /// 创建环形截面
        /// </summary>
        /// <param name="p">创建截面所在的平面</param>
        /// <param name="r_out">外径</param>
        /// <param name="r_in">内径</param>
        /// <returns></returns>
        public CurveArrArray CreateLoopSection(Plane p, double r_out, double r_in)
        {
            CurveArrArray caa = new CurveArrArray();
            r_out = mmToFeet(r_out);
            r_in = mmToFeet(r_in);

            Arc outcircle = Arc.Create(p, r_out, 0, 2 * Math.PI);
            Arc incircle = Arc.Create(p, r_in, 0, 2 * Math.PI);

            CurveArray curveArr1 = new CurveArray();
            curveArr1.Append(outcircle);
            curveArr1.Append(incircle);

            m_familyCreator.NewModelCurveArray(curveArr1, SketchPlane.Create(doc, p));

            caa.Append(curveArr1);

            return caa;
        }

        /// <summary>
        /// 创建正N边型截面，第一点落在y轴上
        /// </summary>
        /// <param name="p">创建截面所在的平面</param>
        /// <param name="r">外接圆半径</param>
        /// <param name="n">边数(n>=3)</param>
        /// <returns></returns>
        public CurveArrArray CreateNPolygonSection(Plane p, double r, int n)
        {
            if (n < 3)
                return null;

            CurveArrArray caa = new CurveArrArray();
            r = mmToFeet(r);
            CurveArray curveArr1 = new CurveArray();

            Transform transform = Transform.CreateReflection(p);
            transform.BasisX = p.XVec;
            transform.BasisY = p.YVec;
            transform.BasisZ = p.Normal;
            transform.Origin = p.Origin;

            for (int i = 0; i < n; i++)
            {
                Line l = Line.CreateBound(transform.OfPoint(new XYZ(Math.Cos(i*2*Math.PI/n),Math.Sin(i*2*Math.PI/n),0)),
                                            transform.OfPoint(new XYZ(Math.Cos((i+1)*2*Math.PI/n),Math.Sin((i+1)*2*Math.PI/n),0)));
                curveArr1.Append(l);
            }

            m_familyCreator.NewModelCurveArray(curveArr1, SketchPlane.Create(doc, p));

            caa.Append(curveArr1);
            return caa;
        }

        public static CurveArray CreateNPolygonSection1(Plane p, double r, int n)
        {
            if (n < 3)
                return null;
            r = Utils.mmToFeet(r);
            CurveArray curveArr1 = new CurveArray();

            Transform transform = Transform.CreateReflection(p);
            transform.BasisX = p.XVec;
            transform.BasisY = p.YVec;
            transform.BasisZ = p.Normal;
            transform.Origin = p.Origin;

            for (int i = 0; i < n; i++)
            {
                Line l = Line.CreateBound(transform.OfPoint(new XYZ(r*Math.Cos(i * 2 * Math.PI / n), r*Math.Sin(i * 2 * Math.PI / n), 0)),
                                            transform.OfPoint(new XYZ(r*Math.Cos((i + 1) * 2 * Math.PI / n), r*Math.Sin((i + 1) * 2 * Math.PI / n), 0)));
                curveArr1.Append(l);
            }

            return curveArr1;
        }

        

        double mmToFeet(double mmVal)
        {
            return mmVal / 304.8;
        }

        /// <summary>
        /// 根据名称查找familysymbol
        /// </summary>
        /// <param name="doc">项目文件</param>
        /// <param name="name">symbol名称</param>
        /// <returns>familysymbol</returns>
        public FamilySymbol getSymbolType(Document doc, string name)
        {
            FilteredElementIdIterator workWellItrator = new FilteredElementCollector(doc).OfClass(typeof(Family)).GetElementIdIterator();
            workWellItrator.Reset();
            FamilySymbol getsymbol = null;
            while (workWellItrator.MoveNext())
            {
                Family family = doc.GetElement(workWellItrator.Current) as Family;
                foreach (ElementId id in family.GetFamilySymbolIds())
                {
                    FamilySymbol symbol = doc.GetElement(id) as FamilySymbol;
                    if (symbol.Name == name)
                    {
                        getsymbol = symbol;
                    }
                }
            }
            return getsymbol;

        }

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
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class CreateVoidCut : IExternalCommand
    {
        Autodesk.Revit.Creation.FamilyItemFactory m_familyCreator;
        Document doc;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            doc = app.ActiveUIDocument.Document;
            m_familyCreator = doc.FamilyCreate;

            //Blend blend = Utils.findElement(doc, typeof(Blend), "融合") as Blend;
            //Blend vaccum = Utils.findElement(doc, typeof(Blend), "空心 融合") as Blend;
            //TaskDialog.Show("1", SolidSolidCutUtils.IsAllowedForSolidCut(blend).ToString());
            //TaskDialog.Show("2", blend.Id.ToString());
            //TaskDialog.Show("1", InstanceVoidCutUtils.CanBeCutWithVoid(blend).ToString());
            //TaskDialog.Show("1", SolidSolidCutUtils.IsAllowedForSolidCut(blend).ToString());
             

            Transaction trans = new Transaction(doc);
            trans.Start("创建空心融合");
            Plane p1 = new Plane(XYZ.BasisZ, new XYZ(0, 0, 0));
            Plane p2 = new Plane(XYZ.BasisZ, new XYZ(0, 0, Utils.mmToFeet(1000)));
            m_familyCreator.NewBlend(true, AssemblePoleSectionInFamily.CreateNPolygonSection1(p2, 100, 16), AssemblePoleSectionInFamily.CreateNPolygonSection1(p1, 200, 16), SketchPlane.Create(doc, p1));
            m_familyCreator.NewBlend(false, AssemblePoleSectionInFamily.CreateNPolygonSection1(p2, 80, 16), AssemblePoleSectionInFamily.CreateNPolygonSection1(p1, 180, 16), SketchPlane.Create(doc, p1));
            //InstanceVoidCutUtils.AddInstanceVoidCut(doc, blend, vaccum);
            //SolidSolidCutUtils.AddCutBetweenSolids(doc, blend, vaccum);
            trans.Commit();

            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class MergeCurve : IExternalCommand
    {
        Document doc;
        Reference firstline;       
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            doc = app.ActiveUIDocument.Document;
            Autodesk.Revit.UI.Selection.Selection sel = app.ActiveUIDocument.Selection;
            firstline = sel.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择起点的曲线");
            IList<Reference> fi_list = sel.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择其他待连接的曲线");

            //将曲线进行排序
            fi_list = SortLine(fi_list);

            //提取曲线上的点
            List<XYZ> points = new List<XYZ>();
            for (int i = 0; i < fi_list.Count; i++)
            {
                FamilyInstance fi = doc.GetElement(fi_list[i]) as FamilyInstance;
                List<XYZ> temp = FindControlPoints(doc, fi.Symbol.Family);
                if (i !=0 )
                {   
                    //判断起点终点是否倒置
                    if (temp[0].DistanceTo(points[points.Count - 1]) > temp[temp.Count - 1].DistanceTo(points[points.Count - 1]))
                        temp.Reverse();
                    //判断起点与上一条线的终点是否重合
                    if (temp[0].IsAlmostEqualTo(points[points.Count - 1]))
                        points.AddRange(temp.GetRange(1, temp.Count - 1));
                    else
                        points.AddRange(temp);
                    continue;
                }
                points.AddRange(temp);
            }

            //绘制曲线
            Transaction trans = new Transaction(doc, "tt");
            trans.Start();
            Document massdoc = doc.Application.NewFamilyDocument(@"C:\ProgramData\Autodesk\RVT 2016\Family Templates\Chinese\概念体量\公制体量.rft");
            Transaction mass = new Transaction(massdoc);
            mass.Start("创建新曲线");
            string filename = "test";
            string foldername = Path.GetDirectoryName(doc.PathName);
            string path = foldername + "\\" + filename + ".rft";
            for (int j = 0; j < 100; j++)
            {
                filename = "排管" + j.ToString();
                path = foldername + "\\" + filename + ".rft";
                FamilySymbol fst = Utils.getSymbolType(doc, filename);
                if (!File.Exists(path) && fst == null)
                    break;
                else continue;
            }

            HermiteSpline curve = CreateCableCurveFamily(massdoc, points) as HermiteSpline;
            mass.Commit();
            massdoc.SaveAs(path);

            //将电缆插入项目文件中
            doc.LoadFamily(path);
            FamilySymbol fs = Utils.getSymbolType(doc, filename);
            fs.Activate();
            FamilyInstance fi2 = doc.Create.NewFamilyInstance(XYZ.Zero, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
            fi2.LookupParameter("起点").Set(curve.GetEndPoint(0).ToString());
            fi2.LookupParameter("起点方向").Set(curve.Tangents[0].ToString());
            fi2.LookupParameter("终点").Set(curve.GetEndPoint(1).ToString());
            fi2.LookupParameter("终点方向").Set(curve.Tangents[curve.Tangents.Count - 1].ToString());
            Autodesk.Revit.DB.View view = doc.ActiveView;
            Category ca = Category.GetCategory(doc, BuiltInCategory.OST_Mass);
            view.SetVisibility(ca, true);

            //删除合并前的曲线
            for (int i = 0; i < fi_list.Count; i++)
                doc.Delete(fi_list[i].ElementId);

            trans.Commit();
            return Result.Succeeded;
        }

        /// <summary>
        /// 找到族中相应的参照点并转换为xyz类型
        /// </summary>
        /// <param name="doc">族所在的项目文档</param>
        /// <param name="family">族类型</param>
        /// <returns>revit中xyz三维坐标的集合</returns>   
        public List<XYZ> FindControlPoints(Document doc, Family family)
        {
            Document fdoc = doc.EditFamily(family);

            //在族文件中找到参照点
            FilteredElementCollector collector = new FilteredElementCollector(fdoc);
            if (collector != null)
                collector.OfClass(typeof(ReferencePoint));
            IList<Element> list = collector.ToElements();

            //将参照点转化为xyz坐标
            List<XYZ> points = new List<XYZ>();
            for (int i = 0; i < list.Count; i++)
            {
                points.Add(((ReferencePoint)list[i]).Position);
            }
            return points;
        }

        public Curve CreateCableCurveFamily(Document massdoc, List<XYZ> points)
        {
            ReferencePointArray ptArr = new ReferencePointArray();
            for (int i = 0; i < points.Count; i++)
            {
                ReferencePoint p = massdoc.FamilyCreate.NewReferencePoint(points[i]);
                ptArr.Append(p);
            }

            CurveByPoints curve = massdoc.FamilyCreate.NewCurveByPoints(ptArr);

            FamilyParameter qidian = massdoc.FamilyManager.AddParameter("起点", BuiltInParameterGroup.PG_TEXT, ParameterType.Text, true);
            massdoc.FamilyManager.AddParameter("起点方向", BuiltInParameterGroup.PG_TEXT, ParameterType.Text, true);
            massdoc.FamilyManager.AddParameter("终点", BuiltInParameterGroup.PG_TEXT, ParameterType.Text, true);
            massdoc.FamilyManager.AddParameter("终点方向", BuiltInParameterGroup.PG_TEXT, ParameterType.Text, true);
            return curve.GeometryCurve as HermiteSpline;
        }

        /// <summary>
        /// 按是否连续对曲线集合进行排序
        /// </summary>
        /// <param name="linelist">曲线集合</param>
        /// <returns>排序后的曲线集合</returns>
        public IList<Reference> SortLine(IList<Reference> linelist)
        {
            IList<Reference> result = new List<Reference>();
            result.Add(firstline);
            Reference lastline = firstline;
            int count = 0;
            int originLength = linelist.Count;
            while (linelist.Count != 0)
            {
                for (int i = 0; i < linelist.Count; i++)
                {
                    if(isAjacent(lastline,linelist[i]))
                    {
                        result.Add(linelist[i]);
                        lastline = linelist[i];
                        linelist.RemoveAt(i);
                    }                    
                }
                count++;
                if (count > originLength)
                    break;
            }
            if (linelist.Count == 0)
                return result;
            return null;
        }

        /// <summary>
        /// 判断两条线是否相连
        /// </summary>
        /// <param name="r1">第一条线</param>
        /// <param name="r2">第二条线</param>
        /// <returns></returns>
        public bool isAjacent(Reference r1, Reference r2)
        {
            XYZ p1 = Utils.stringToXYZ(((FamilyInstance)doc.GetElement(r1)).LookupParameter("起点").AsString());
            XYZ p2 = Utils.stringToXYZ(((FamilyInstance)doc.GetElement(r1)).LookupParameter("终点").AsString());
            XYZ p3 = Utils.stringToXYZ(((FamilyInstance)doc.GetElement(r2)).LookupParameter("起点").AsString());
            XYZ p4 = Utils.stringToXYZ(((FamilyInstance)doc.GetElement(r2)).LookupParameter("终点").AsString());
            return p2.IsAlmostEqualTo(p3) || p1.IsAlmostEqualTo(p4) || p2.IsAlmostEqualTo(p4) || p1.IsAlmostEqualTo(p3);
        }

    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class FindPoints : IExternalCommand
    {
        Document doc;
        Document f_doc;
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            doc = app.ActiveUIDocument.Document;
            //找到排管的族文件
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            if (collector != null)
                collector.OfClass(typeof(Family));
            IList<Element> list = collector.ToElements();
            foreach (Element f in list)
            {
                Family family = f as Family;
                if (family.Name == "排管0")
                {
                    f_doc = doc.EditFamily(family);
                    break;
                }
            }

            //找到排管的起点方向startDir
            String coordinate;
            XYZ startDir;
            FamilyInstance fi = Utils.findElement(doc, typeof(FamilyInstance), "排管0") as FamilyInstance;
            coordinate = fi.LookupParameter("起点方向").AsString();
            TaskDialog.Show("1", coordinate);
            startDir = Utils.stringToXYZ(coordinate);

            //在族文件中找到参照点
            FilteredElementCollector collector1 = new FilteredElementCollector(f_doc);
            if (collector1 != null)
                collector1.OfClass(typeof(ReferencePoint));
            IList<Element> list1 = collector1.ToElements();

            //将参照点转化为xyz坐标
            List<XYZ> points = new List<XYZ>();
            for (int i = 0; i < list1.Count; i++)
            {
                points.Add(((ReferencePoint)list1[i]).Position);
            }

            //在项目文件中绘制电缆
            CableClimb.CableClimb cc = new CableClimb.CableClimb();
            Transaction trans = new Transaction(doc);
            trans.Start("创建电缆");
            cc.CreateFlexDuct(doc, points, 30, startDir, XYZ.BasisX);
            trans.Commit();

            return Result.Succeeded;
        }
    }

    public class Utils
    {
        public static double mmToFeet(double mmVal)
        {
            return mmVal / 304.8;
        }

        /// <summary>
        /// 根据名称查找familysymbol
        /// </summary>
        /// <param name="doc">项目文件</param>
        /// <param name="name">symbol名称</param>
        /// <returns>familysymbol</returns>
        public static FamilySymbol getSymbolType(Document doc, string name)
        {
            FilteredElementIdIterator workWellItrator = new FilteredElementCollector(doc).OfClass(typeof(Family)).GetElementIdIterator();
            workWellItrator.Reset();
            FamilySymbol getsymbol = null;
            while (workWellItrator.MoveNext())
            {
                Family family = doc.GetElement(workWellItrator.Current) as Family;
                foreach (ElementId id in family.GetFamilySymbolIds())
                {
                    FamilySymbol symbol = doc.GetElement(id) as FamilySymbol;
                    if (symbol.Name == name)
                    {
                        getsymbol = symbol;
                    }
                }
            }
            return getsymbol;

        }

        public static Element findElement(Document doc, Type targetType, string targetName)
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

        public static string getVaule(FamilyInstance fi, string para)
        {
            IList<Parameter> list = fi.GetParameters(para);
            string result = "";
            if (list.Count > 0)
                result = list[0].AsValueString();
            return result;
        }

        /// <summary>
        /// 将坐标由string转化为xyz
        /// </summary>
        /// <param name="s">坐标的字符串</param>
        /// <returns>revit中xyz坐标</returns>
        public static XYZ stringToXYZ(string s)
        {
            s = s.Substring(1, s.Length - 2);
            string[] coordinate = s.Split(',');
            if(coordinate.Length == 3)
                return new XYZ(Double.Parse(coordinate[0]), Double.Parse(coordinate[1]), Double.Parse(coordinate[2]));
            return null;
        }

    }
}
