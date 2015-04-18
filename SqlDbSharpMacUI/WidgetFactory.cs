// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using MonoMac.AppKit;
using System.Drawing;

namespace SqlDbSharpMacUI.Extensions
{
    public static class WidgetFactory
    {
        private static void _sharedCode(NSTextView txt, NSScrollView scrollView)
        {
            txt.Frame = new RectangleF (50, 50, 50, 50);    // This keeps it from being 0,0,0,0 after resizing starts for some reason.

            scrollView.ScrollerStyle = NSScrollerStyle.Overlay;
            scrollView.AutohidesScrollers = false;
            scrollView.DocumentView = txt;

            // Interesting, but not what I want.
            //            this.scrollSql.HasHorizontalRuler = true;
            //            this.scrollSql.HasVerticalRuler = true;
            //            this.scrollSql.RulersVisible = true;
        }

        public static void AddVerticalScrollbar(this NSTextView txt, NSScrollView scrollView)
        {
            _sharedCode (txt, scrollView);

            // https://developer.apple.com/library/mac/documentation/Cocoa/Conceptual/TextUILayer/Tasks/TextInScrollView.html
            scrollView.HasVerticalScroller = true;
            scrollView.HasHorizontalScroller = false;
            scrollView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;

            SizeF contentSize = scrollView.ContentSize;

            //=========================================================================================
            // Vertical Scrollbar only -- works
            //=========================================================================================
            txt.MinSize = new SizeF (0, contentSize.Height);
            txt.MaxSize = new SizeF (float.MaxValue, float.MaxValue);
            txt.VerticallyResizable = true;
            txt.HorizontallyResizable = false;
            txt.AutoresizingMask = NSViewResizingMask.WidthSizable;
            txt.TextContainer.ContainerSize = new SizeF (contentSize.Width, float.MaxValue);
            txt.TextContainer.WidthTracksTextView = true;
            //=========================================================================================
            //=========================================================================================
        }

        // This is acting really squirrelly on resize. The horizontal scrollbar goes away
        // until you edit a line that'd make it reappear. That includes a line that was already
        // "too long" and required scrolling. So add until horizontal scroll, bar appears, resize,
        // bar disappears, add a space to that previous line, and it's back. /shrug
        public static void AddBothScrollbars(this NSTextView txt, NSScrollView scrollView)
        {
            _sharedCode (txt, scrollView);

            scrollView.HasVerticalScroller = true;
            scrollView.HasHorizontalScroller = true;
            scrollView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;

            txt.MinSize = new SizeF (0, 0);
            txt.MaxSize = new SizeF (float.MaxValue, float.MaxValue);
            txt.VerticallyResizable = true;
            txt.HorizontallyResizable = true;
            txt.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
            txt.TextContainer.ContainerSize = new SizeF (float.MaxValue, float.MaxValue);
            txt.TextContainer.WidthTracksTextView = false;

            // [[theTextView enclosingScrollView] setHasHorizontalScroller:YES];
            // [theTextView setHorizontallyResizable:YES];
            // [theTextView setAutoresizingMask:(NSViewWidthSizable | NSViewHeightSizable)];
            // [[theTextView textContainer] setContainerSize:NSMakeSize(FLT_MAX, FLT_MAX)];
            // [[theTextView textContainer] setWidthTracksTextView:NO];
        }

    }
}

