using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AutoAlignBlock
{
    public class Command
    {
        [CommandMethod("AutoAlignBlock")]
        public void AlignBlock()
        {
            var doc = AcAp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // Let user select multiple blocks
                PromptSelectionOptions selOpt = new PromptSelectionOptions();
                selOpt.MessageForAdding = "Select object to align:";
                selOpt.AllowDuplicates = false;
                selOpt.AllowSubSelections = false;
                PromptSelectionResult selRes = ed.GetSelection(selOpt);

                if (selRes.Status != PromptStatus.OK || selRes.Value.Count == 0) return;
                SelectionSet blocksToAlignSet = selRes.Value;
                //Let user select source object
                PromptSelectionOptions selSourceObject = new PromptSelectionOptions
                {
                    MessageForAdding = "\nSelecte a block/object to get reference position:",
                    SingleOnly = true, // Chỉ cho phép chọn 1 đối tượng
                    AllowSubSelections = false,
                };
                PromptSelectionResult selReSourceObj = ed.GetSelection(selSourceObject);

                if (selReSourceObj.Status != PromptStatus.OK || selReSourceObj.Value.Count == 0)
                {
                    ed.WriteMessage("\nNo selected object.Cancel.");
                    return;
                }
                SelectedObject sourceSelectedObj = selReSourceObj.Value.OfType<SelectedObject>().FirstOrDefault();
                if (sourceSelectedObj == null)
                {
                    ed.WriteMessage("\nCan not get source object");
                    return;
                }

                // Lấy điểm chèn (Position) của đối tượng gốc
                Point3d sourceBasePoint;
                Entity sourceEntity = tr.GetObject(sourceSelectedObj.ObjectId, OpenMode.ForRead) as Entity;

                if (sourceEntity is BlockReference sourceBlockRef)
                {
                    sourceBasePoint = sourceBlockRef.Position;
                }
                else if (sourceEntity is DBText sourceDbText) // Xử lý nếu chọn Text (MText, DBText)
                {
                    sourceBasePoint = sourceDbText.Position;
                }
                else if (sourceEntity is MText sourceMText) // Thêm hỗ trợ MText
                {
                    sourceBasePoint = sourceMText.Location; // MText có thuộc tính Location
                }
                else if (sourceEntity is Circle sourceCircle) // Xử lý nếu chọn Circle
                {
                    sourceBasePoint = sourceCircle.Center;
                }
                else if (sourceEntity is Line sourceLine) // Xử lý nếu chọn Line (lấy điểm đầu)
                {
                    sourceBasePoint = sourceLine.StartPoint;
                }
                // Thêm các loại đối tượng khác nếu cần
                else
                {
                    ed.WriteMessage("\nSource object is NOT Block or Text.");
                    return;
                }
                // Let user choose alignment direction
                PromptKeywordOptions optDir = new PromptKeywordOptions("\nAlign blocks along [Vertical/Horizontal]:");
                optDir.Keywords.Add("Vertical");
                optDir.Keywords.Add("Horizontal");
                optDir.AllowNone = false;

                PromptResult resDir = ed.GetKeywords(optDir);

                if (resDir.Status != PromptStatus.OK) return;

                bool alignX = (resDir.StringResult == "Vertical");
                int alignedCount = 0;
                foreach (SelectedObject selObj in blocksToAlignSet)
                {
                    Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) as Entity;
                    if (ent is BlockReference blockRef)
                    {
                        Point3d currentPosition = blockRef.Position;
                        Point3d targetPosition;

                        if (alignX)
                        {
                            // Căn chỉnh theo trục X: Giữ Y và Z hiện tại của block, lấy X từ source
                            targetPosition = new Point3d(sourceBasePoint.X, currentPosition.Y, currentPosition.Z);
                        }
                        else // alignY
                        {
                            // Căn chỉnh theo trục Y: Giữ X và Z hiện tại của block, lấy Y từ source
                            targetPosition = new Point3d(currentPosition.X, sourceBasePoint.Y, currentPosition.Z);
                        }
                        Vector3d displacement = targetPosition - currentPosition;

                        // Tạo ma trận dịch chuyển
                        Matrix3d transformMatrix = Matrix3d.Displacement(displacement);

                        // Áp dụng ma trận biến đổi cho BlockReference
                        // Phương thức TransformBy() sẽ di chuyển block và tất cả các thuộc tính của nó
                        blockRef.TransformBy(transformMatrix);

                        alignedCount++;
                    }
                }
                tr.Commit();
                ed.WriteMessage($"\nAlign completed {alignedCount} block.");
            }
        }
    }
}

