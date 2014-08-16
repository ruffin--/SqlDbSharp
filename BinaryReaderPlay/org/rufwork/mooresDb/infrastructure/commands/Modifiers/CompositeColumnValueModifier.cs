using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.extensions;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.serializers;

namespace org.rufwork.mooresDb.infrastructure.commands.Modifiers
{
    public class CompositeColumnValueModifier
    {
        #region convenienceStaticFunctions
        public static bool ColsAreCompatible(Column colToUpdate, Column colSource)
        {
            bool bCompatible = false;

            if (colToUpdate.colType.Equals(COLUMN_TYPES.AUTOINCREMENT))
            {
                bCompatible = false;    // hard stop.  Can't update autoincrement fields.
            }
            else if (colToUpdate.colType.Equals(colSource.colType))
            {
                bCompatible = true;
            }
            else
            {
                // TODO: Are there other sets we want to consider equivalent?
                Queue<COLUMN_TYPES[]> qLinkedTypes = new Queue<COLUMN_TYPES[]>();
                COLUMN_TYPES[] sharedIntTypes = { COLUMN_TYPES.INT, COLUMN_TYPES.AUTOINCREMENT, COLUMN_TYPES.TINYINT };
                qLinkedTypes.Enqueue(sharedIntTypes);

                foreach (COLUMN_TYPES[] relatedColTypes in qLinkedTypes)
                {
                    if (sharedIntTypes.Contains(colToUpdate.colType) && sharedIntTypes.Contains(colSource.colType))
                    {
                        bCompatible = true;
                        break;
                    }
                }
            }
            return bCompatible && colToUpdate.intColLength >= colSource.intColLength;
        }

        // TODO: For now, we're stogily assuming `val[whitespace][plus or minus][whitespace][value]` etc.
        // TODO: Even though we're allowing multiple column names and values, this is still pretty naive,
        //      as we're not handling parentheses or order of operations at all.
        public static byte[] ConstructValue(Column colOutput, string strClause, byte[] abytRowOfValues, TableContext table)
        {
            string strOrigClause = strClause;
            strClause = "+ " + strClause;

            string[] astrTokens = strClause.Split();
            if (0 != astrTokens.Length % 2)
            {
                throw new Exception("Illegal update clause (value-operator count mismatch): " + strOrigClause);
            }

            Queue<CompositeColumnValueModifier> qModifiers = new Queue<CompositeColumnValueModifier>();

            for (int i = 0; i < astrTokens.Length; i = i + 2)
            {
                qModifiers.Enqueue(
                    new CompositeColumnValueModifier(
                        astrTokens[i + 1].IsNumeric() ? astrTokens[i + 1] : string.Empty,
                        astrTokens[i + 1].IsNumeric() ? null : table.getColumnByName(astrTokens[i + 1]),
                        !astrTokens[i].Equals("-")
                    )
                );
            }

            // I realize I could've done this in the loop where I construct 
            // the UpdateModifiers, but this feels a little cleaner.
            byte[] abytResult = new byte[colOutput.intColLength];
            BaseSerializer outputSerializer = Router.routeMe(colOutput);
            foreach (CompositeColumnValueModifier modifier in qModifiers)
            {
                if (modifier.isValueNotColumn)
                {
                    abytResult = outputSerializer.addRawToStringRepresentation(abytResult, modifier.strValue);
                }
                else
                {
                    if (colOutput.intColLength < modifier.column.intColLength || !ColsAreCompatible(colOutput, modifier.column))
                    {
                        throw new Exception("Value aggregation attempted to aggregate values that were potentially too large or with columns of incompatible types.");
                    }
                    byte[] abytValToAdd = new byte[modifier.column.intColLength];
                    Array.Copy(abytRowOfValues, modifier.column.intColStart, abytValToAdd, 0, modifier.column.intColLength);

                    abytResult = outputSerializer.addRawToRaw(abytResult, abytValToAdd, !modifier.isAdditionModifierNotSubtraction);
                }
            }

            return abytResult;
        }
        #endregion convenienceStaticFunction

        #region conventionalClass
        public bool isAdditionModifierNotSubtraction = true;
        public bool isValueNotColumn = true;

        private string _value = string.Empty;
        public string strValue
        {
            get
            {
                return _value;
            }
            set
            {
                _column = null;
                this.isValueNotColumn = true;
                _value = value;
            }
        }

        private Column _column = null;
        public Column column
        {
            get
            {
                return _column;
            }
            set 
            {
                _value = string.Empty;
                this.isValueNotColumn = false;
                _column = value;
            }
        }

        public CompositeColumnValueModifier(string strValue, Column column, bool isAdditionModifierNotSubtraction)
        {
            if (string.IsNullOrWhiteSpace(strValue))
            {
                if (null == column)
                {
                    throw new Exception("Update modifiers must have a value or column to initialize.");
                }

                this.column = column;
            }
            else
            {
                this.strValue = strValue;
            }
            this.isAdditionModifierNotSubtraction = isAdditionModifierNotSubtraction;
        }
        #endregion conventionalClass
    }
}