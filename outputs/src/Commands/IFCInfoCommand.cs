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
                    var link = doc.GetElement(ElementIds.Create(option.Id)) as RevitLinkInstance;
                    if (link?.GetLinkDocument() == null)
                        throw new InvalidOperationException("Link chưa được load.");
                    CategoryReader.Configure(dialog, link.GetLinkDocument(), option);
                    DuctWorkflow.Configure(dialog, doc, link);
                    selectedLink = link;
                };
                PlacementCatalog.Configure(dialog, doc);
                dialog.NavigateSource = (row, action) =>
                {
                    if (selectedLink == null)
                        throw new InvalidOperationException("Chưa chọn IFC link đã load.");
                    DuctNavigation.Execute(uiDoc, selectedLink, row, action);
                };
                new System.Windows.Interop.WindowInteropHelper(dialog).Owner = commandData.Application.MainWindowHandle;
                dialog.ShowDialog();
                if (selectedLink != null)
                {
                    if (dialog.NavigationRow != null)
                    {
                        DuctNavigation.Execute(uiDoc, selectedLink, dialog.NavigationRow, dialog.NavigationAction);
                        return Result.Succeeded;
                    }
                    NativePlacement.Execute(uiDoc, selectedLink, dialog);
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
