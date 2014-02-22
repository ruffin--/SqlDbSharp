// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using org.rufwork.mooresDb.infrastructure.tableParts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace org.rufwork.mooresDb.infrastructure.serializers
{
    class Router
    {
        public static BaseSerializer routeMe(Column colToRoute)
        {
            BaseSerializer serializer = null;

            switch (colToRoute.colType)
            {
                case COLUMN_TYPES.INT:
                case COLUMN_TYPES.TINYINT:
                case COLUMN_TYPES.AUTOINCREMENT:
                    serializer = new IntSerializer(colToRoute);
                    break;

                case COLUMN_TYPES.CHAR:
                    serializer = new CharSerializer(colToRoute);
                    break;

                // For now, I'm going to cheese out and treat these both
                // as decimal.  As Skeet says [1], they're both *floating point*
                // representations, even if we don't normally think of FLOATS
                // the same as we do decimals.
                // [1] http://tinyurl.com/mzu6t4j
                // TODO: Create a real float type for huge numbers.
                case COLUMN_TYPES.FLOAT:
                case COLUMN_TYPES.DECIMAL:
                    serializer = new DecimalSerializer(colToRoute);
                    break;

                // TODO: Create a real BIT column type with serializer.
                case COLUMN_TYPES.BIT:
                    serializer = new IntSerializer(colToRoute);
                    break;

                case COLUMN_TYPES.DATETIME:
                    serializer = new DateSerializer(colToRoute);
                    break;

                default:
                    throw new Exception("Illegal/Unimplemented column type: " + colToRoute.colType);
            }

            return serializer;
        }
    }
}
