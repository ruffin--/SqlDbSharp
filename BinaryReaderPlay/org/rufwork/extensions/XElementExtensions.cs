// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace org.rufwork.extensions
{
    public static class XElementExtensions
    {
        // Just to remove the xElement.Attribute("name") == null ? null : xElement.Attribute("name").Value cruft.
        // Let's also return an empty string on null.
        public static string AttSafeVal(this XElement xElement, string strAttribName, bool returnNullNotEmptyString = false)
        {
            string strReturn = string.Empty;

            if (returnNullNotEmptyString)
            {
                strReturn = null;
            }

            if (xElement.Attribute(strAttribName) != null)
            {
                strReturn = xElement.Attribute(strAttribName).Value.ToString();
            }

            return strReturn;
        }

        public static string SafeChildVal(this XElement xElement, string strChildName, XDocument xDoc = null)
        {
            XElement child = null;
            if (xDoc == null)
            {
                child = xElement.Elements(strChildName).FirstOrDefault();
            }
            else
            {
                child = xElement.Elements(xDoc.Root.GetDefaultNamespace() + strChildName).FirstOrDefault();
            }
            return child != null ? child.Value : null;
        }

        // TODO: There's probably some cycle cost for moving from xDoc to Root to the default namespace each time
        // that we eventually want to clear out.  You could even stick that string in a static var that gets used
        // here, but that smells a little funky[er].
        public static IEnumerable<XElement> NSxElements(this XElement xElement, XDocument xDoc, string strElementSelector)
        {
            return xElement.Elements(xDoc.Root.GetDefaultNamespace() + strElementSelector);
        }
    }
}




