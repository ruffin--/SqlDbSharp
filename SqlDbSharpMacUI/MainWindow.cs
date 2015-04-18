// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

// This project intends to work around the bug with Xamarin 
// on OS X when using ISQL detailed here:
// https://bugzilla.xamarin.com/show_bug.cgi?format=multiple&id=22262
using System;
using System.Collections.Generic;
using System.Linq;
using MonoMac.Foundation;
using MonoMac.AppKit;
using System.Drawing;

using SqlDbSharpMacUI.Extensions;

using org.rufwork.mooresDb.infrastructure;

namespace SqlDbSharpMacUI
{
    public partial class MainWindow : MonoMac.AppKit.NSWindow
    {
        float fltFullHeight = 0;
        float fltFullWidth = 0;

        NSButton cmdGo =  new NSButton();
        NSTextView txtSql = new NSTextView ();
        NSTextView txtResults = new NSTextView ();

        NSScrollView scrollSql = new NSScrollView ();
        NSScrollView scrollResults = new NSScrollView ();

        DbInteractions db = null;

        #region Constructors

        // Called when created from unmanaged code
        public MainWindow (IntPtr handle) : base (handle)
        {
            Initialize ();

            string strParentDir = string.Empty;
            string strConfigFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SqlDbSharp.config");
            if (System.IO.File.Exists(strConfigFile))
            {
                strParentDir = System.IO.File.ReadAllText(strConfigFile);
                strParentDir = strParentDir.TrimEnd(System.Environment.NewLine.ToCharArray());
            }
            else
            {
                // set up debug db
                strParentDir = org.rufwork.Utils.cstrHomeDir + System.IO.Path.DirectorySeparatorChar + "MooresDbPlay";
            }
                
            this.db = new DbInteractions (strParentDir);
            this.txtResults.Value = @"SqlDb# ISQL client.
SqlDb# version: " + org.rufwork.mooresDb.MainClass.version + @"

Type one or more statements terminated by semi-colons. Press the ""Execute SQL"" or F5 to execute.

Starting at testing dir: " + strParentDir + @"

You may set a start-up database by including the full path on a single line in a file called 
SqlDbSharp.config, placed in this folder:

     " + Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"
";
        }
        
        // Called when created directly from a XIB file
        [Export ("initWithCoder:")]
        public MainWindow (NSCoder coder) : base (coder)
        {
            Initialize ();
        }
        
        // Shared initialization code
        void Initialize ()
        {
            this.initUI ();
            this.DidResize += HandleWindowDidResize;
        }

        void HandleWindowDidResize (object sender, EventArgs e)
        {
            this.sizeUI ();
        }

        public void sizeUI()
        {
            fltFullHeight = this.ContentView.Frame.Size.Height;
            fltFullWidth = this.ContentView.Frame.Size.Width;

            float xSpacer = 5;
            float ySpacer = 5;
            float fltButtonHeight = 25F;

            float fromBottom = ySpacer;

            float fltTextViewsHeight = (fltFullHeight - (4 * ySpacer) - fltButtonHeight) / 2;
            RectangleF rectResults = new RectangleF(xSpacer, fromBottom,
                fltFullWidth - (2 * xSpacer), fltTextViewsHeight);
            this.scrollResults.Frame = rectResults;
            this.txtResults.Frame = rectResults;

            fromBottom += fltTextViewsHeight + ySpacer;

            Console.WriteLine (fltFullHeight + " :: " + fltFullWidth);
            cmdGo.Frame = new RectangleF(0, fromBottom, 150, fltButtonHeight);

            fromBottom += fltButtonHeight + ySpacer;

            RectangleF rectSqlEdit = new RectangleF(xSpacer, fromBottom,
                fltFullWidth - (2 * xSpacer), fltTextViewsHeight);
            this.scrollSql.Frame = rectSqlEdit;
            //this.txtSql.Frame = rectSqlEdit;

        }

        public void initUI()
        {
            this.Title = "MacISQL for SqlDbSharp (c) 2015 -- USE AT YOUR OWN RISK";
            cmdGo.Title = "Execute SQL (F5)";
            cmdGo.BezelStyle = MonoMac.AppKit.NSBezelStyle.Rounded;
            cmdGo.Activated += (object sender, EventArgs e) => {
                Console.WriteLine((char)MonoMac.AppKit.NSKey.F5);
                string strResults = this.db.processCommand(this.txtSql.Value);
                this.txtResults.Value = strResults;
            };
            cmdGo.KeyEquivalent = ((char)MonoMac.AppKit.NSKey.F5).ToString ();

            this.txtSql.AddBothScrollbars (this.scrollSql);
            this.txtSql.Font = NSFont.UserFixedPitchFontOfSize (10F);
            this.txtResults.AddBothScrollbars (this.scrollResults);
            this.txtResults.Font = NSFont.UserFixedPitchFontOfSize (10F);

            this.ContentView.AddSubview (cmdGo);
            this.ContentView.AddSubview (this.scrollSql);
            this.ContentView.AddSubview (this.scrollResults);

            this.sizeUI ();
        }
        #endregion
    }
}

