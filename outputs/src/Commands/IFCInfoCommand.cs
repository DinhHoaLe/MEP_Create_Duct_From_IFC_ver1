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
                TaskDialog.Show("IFC Info", "Open a Revit project first.");
                return Result.Cancelled;
            }

            // Check if the IFC link catalog is available

            try
            {
                Document doc = uiDoc.Document;
                var links = IfcLinkCatalog.GetLinks(doc);
                long? lastLinkId = null;
                long? lastCategoryId = null;
                CategoryReviewSession lastReview=null;
                DashboardSelection dashboardChoice=null;
                bool showDashboard=true;
                while (true)
                {
                    if(showDashboard)
                    {
                        var dashboard=new WorkflowDashboard(dashboardChoice);
                        new System.Windows.Interop.WindowInteropHelper(dashboard).Owner=commandData.Application.MainWindowHandle;
                        if(dashboard.ShowDialog()!=true)break;
                        var nextChoice=dashboard.Selection;
                        if(dashboardChoice==null || dashboardChoice.Summary!=nextChoice.Summary)
                        {
                            lastCategoryId=null;lastReview=null;
                        }
                        dashboardChoice=nextChoice;showDashboard=false;
                    }
                    var dialog = new IFCInfoWindow();
                    dialog.DashboardChoice=dashboardChoice;
                    dialog.Links = links;
                    dialog.InitialLinkId = lastLinkId;
                    dialog.InitialCategoryId = lastCategoryId;
                    dialog.InitialReview=lastReview;
                    RevitLinkInstance selectedLink = null;
                    dialog.LoadLink = option =>
                    {
                        selectedLink = null;
                        var link = doc.GetElement(ElementIds.Create(option.Id)) as RevitLinkInstance;
                        if (link?.GetLinkDocument() == null)
                            throw new InvalidOperationException("The link is unloaded.");
                        CategoryReader.Configure(dialog, link.GetLinkDocument(), option);
                        dialog.Categories=dialog.Categories.Where(dashboardChoice.Includes).ToList();
                        DuctWorkflow.Configure(dialog, doc, link);
                        selectedLink = link;
                    };
                    PlacementCatalog.Configure(dialog, doc);
                    dialog.RunDuctCreation=()=>
                    {
                        if(selectedLink==null)throw new InvalidOperationException("No loaded IFC link is selected.");
                        try {DuctCreation.Execute(uiDoc,dialog);}
                        finally {
                            DuctExistenceChecker.Update(dialog,doc,selectedLink);
                            PlacementCatalog.Configure(dialog,doc);
                        }
                    };
                    dialog.NavigateSource = (row, action) =>
                    {
                        if (selectedLink == null)
                            throw new InvalidOperationException("No loaded IFC link is selected.");
                        DuctNavigation.Execute(uiDoc, selectedLink, row, action);
                    };
                    new System.Windows.Interop.WindowInteropHelper(dialog).Owner = commandData.Application.MainWindowHandle;
                    dialog.ShowDialog();
                    if(dialog.DashboardRequested)
                    {
                        lastLinkId=dialog.SelectedLink?.Id;
                        lastCategoryId=dialog.SelectedCategory?.Id;
                        lastReview=dialog.ExportReviewSession();
                        showDashboard=true;
                        continue;
                    }
                    bool hasRequest = dialog.DuctCreationRequest != null || dialog.Replacement != null;
                    if (selectedLink == null || (!hasRequest && dialog.NavigationRow == null))
                        break;
                    lastLinkId = dialog.SelectedLink?.Id;
                    lastCategoryId = dialog.SelectedCategory?.Id;
                    lastReview=dialog.ExportReviewSession();
                    try
                    {
                        if (dialog.NavigationRow != null)
                        {
                            DuctNavigation.Execute(uiDoc, selectedLink, dialog.NavigationRow, dialog.NavigationAction);
                            continue;
                        }
                        NativePlacement.Execute(uiDoc, selectedLink, dialog);
                        DuctCreation.Execute(uiDoc, dialog);
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // Return to the refreshed source list after a cancelled selection.
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Create Duct from IFC", ex.Message);
                    }
                    // Rebuild the source list against the committed model before another run.
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
