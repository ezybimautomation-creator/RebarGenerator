using System;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace ToolsByGimhan.RebarGenerator
{
    public class ToolsByGimhanApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                CreateRibbonUI(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Rebar Generator Startup Error", ex.ToString());
                return Result.Failed;
            }
        }

        // Extracted to a separate method so WPF dependencies don't crash OnStartup JIT compilation
        private void CreateRibbonUI(UIControlledApplication application)
        {
            string tabName = "Tools By Gimhan";
            try { application.CreateRibbonTab(tabName); } catch { }

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Rebar");
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "cmdRebarGenerator",
                "Rebar\nGenerator",
                assemblyPath,
                "ToolsByGimhan.RebarGenerator.RebarGeneratorCommand")
            {
                ToolTip = "Automatically models rebar in a selected beam/column based on user input."
            };

            if (panel.AddItem(buttonData) is PushButton button)
            {
                string iconPath = Path.Combine(Path.GetDirectoryName(assemblyPath) ?? "", "icon.jpg");
                if (File.Exists(iconPath))
                {
                    Uri iconUri = new Uri(iconPath);
                    BitmapImage bitmap = new BitmapImage(iconUri);
                    button.LargeImage = bitmap;
                    button.Image = bitmap;
                }
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
