using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;


namespace IFCInfo
{
    [Transaction(TransactionMode.Manual)]
    public class IFCInfoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
            ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("IFC Info", "Hãy mở một project Revit trước.");
                return Result.Cancelled;
            }

            // Check if the IFC link catalog is available

            try
            {
                Document doc = uiDoc.Document;
                var dialog = new IFCInfoWindow();
                dialog.Links = IfcLinkCatalog.GetLinks(doc);
                RevitLinkInstance selectedLink = null;
                dialog.LoadLink = option =>
                {
                    selectedLink = null;
                    var link = doc.GetElement(new ElementId(option.Id)) as RevitLinkInstance;
                    if (link?.GetLinkDocument() == null)
                        throw new InvalidOperationException("Link chưa được load.");
                    CategoryReader.Configure(dialog, link.GetLinkDocument(), option.IfcPath);
                    DuctWorkflow.Configure(dialog, doc, link);
                    selectedLink = link;
                };
                AirTerminalReplacement.Configure(dialog, doc);
                new System.Windows.Interop.WindowInteropHelper(dialog).Owner = commandData.Application.MainWindowHandle;
                dialog.ShowDialog();
                if (selectedLink != null)
                {
                    AirTerminalReplacement.Execute(uiDoc, selectedLink, dialog);
                    DuctCreation.Execute(uiDoc, dialog);
                }
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = "IFC Info: " + ex.Message;
                return Result.Failed;
            }
        }

    }
}
