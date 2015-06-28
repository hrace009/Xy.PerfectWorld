﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Xy.PerfectWorld.ViewModels;

namespace Xy.PW
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // show an error report for release version, and prevent wrong version being shipped
#if !DEBUG
            DispatcherUnhandledException += (s, args) => ReportUnhandledException(args.Exception);
#else
            if (Environment.UserName != "Xiaoy")
            {
                MessageBox.Show("Please contact your administrator.", "Incorrect Software Version", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
            }
#endif
            if (!License.CheckLicense())
            {
                var cpuID = License.GetCpuID();
                Clipboard.SetText(cpuID);
                MessageBox.Show($"Please contact your administrator with your CpuID. It has been copied to your clipbaord.\n CpuID : {cpuID}", "Unlicensed Software", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Shutdown();
            }


            // check admin rights
            var isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            if (!isAdmin)
            {
                MessageBox.Show("XyPW requires administrative rights to work. Please restart as admin.", this.GetType().Namespace, MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
            }

            var view = new MainView() { DataContext = AppViewModel.Instance };
            view.Show();


            //var view = this.Windows.OfType<SettingView>().SingleOrDefault() ??
            //    new SettingView() { DataContext = AppViewModel.SettingVM };

            //view.Show();
        }

        private void ReportUnhandledException(Exception exception)
        {
            Func<Exception, XElement> convertToXml = null;
            convertToXml = e => e == null ? null :
                new XElement(e.GetType().Name,
                    new XElement("FullName", e.GetType().FullName),
                    new XElement("Message", e.Message),
                    new XElement("Source", e.Source),
                    new XElement("TargetSite", e.TargetSite),
                    new XElement("StackTrace", e.StackTrace),
                    new XElement("InnerException", convertToXml(e.InnerException))
                    );

            var path = Path.GetFullPath($"error log/{DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss")}.xml");
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            convertToXml(exception).Save(path);

            var message = "An error log has been produced for this error, which can be found at \n" + path + "\n" +
                "Here is an summary of the error : \n\n" + exception;
            MessageBox.Show(message, "Something went wrong...", MessageBoxButton.OK, MessageBoxImage.Error);

            Shutdown();
        }
    }
}
