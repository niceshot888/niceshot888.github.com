﻿using Dapper;
using MySql.Data.MySqlClient;
using NiceShot.Core.Enums;
using NiceShot.Core.Helper;
using NiceShot.Core.Models.Entities;
using NiceShot.Core.Models.JsonObjects;
using NiceShot.Core.Models.Structs;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace NiceShot.Core.DataAccess
{
    public class EMDataAccessV1
    {

        #region 生成增删改SQL语句

        /// <summary>
        /// 生成操作财务报表SQL代码
        /// </summary>
        public static void BuildOperTableSQLCode()
        {
            var dirName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files");
            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);

            var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files", "em_all_rep_oper.cs");
            if (File.Exists(fileName))
                File.WriteAllText(fileName, string.Empty);

            using (StreamWriter sw = new StreamWriter(fileName))
            {
                for (int reportType = 1; reportType <= 3; reportType++)
                {
                    var tmpl_oper = GetOperTableSQLCode((ReportType)reportType);
                    sw.WriteLine(tmpl_oper);
                }
            }
        }

        private static string GetOperTableSQLCode(ReportType reportType)
        {
            var tableName = GetTableName(reportType);

            var tmpl_oper = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "oper_em_findata_v1.txt"));
            tmpl_oper = tmpl_oper.Replace("$tablename$", tableName);
            tmpl_oper = tmpl_oper.Replace("$insert_sql$", GetReportInsertSql(reportType, new List<string>() { }));
            tmpl_oper = tmpl_oper.Replace("$update_sql$", GetReportUpdateSql(reportType, new List<string>() { }));
            tmpl_oper = tmpl_oper.Replace("$assign_data$", GetAssignEntityCode(reportType, new List<string>() { }));

            return tmpl_oper;
        }

        private static string GetAssignEntityCode(ReportType reportType, List<string> ignoreFields)
        {
            StringBuilder sw = new StringBuilder();
            var dataFieldList = GetTableDataFieldList(reportType);
            foreach (var field in dataFieldList)
            {
                if (field.FieldName != "id" && ignoreFields.Contains(field.FieldName.ToLower()))
                    continue;

                if (field.DataType == "date")
                    sw.AppendLine($"{field.FieldName.ToLower()} = jo.{field.FieldName}.ConvertToDate(),");
                else if (field.DataType == "varchar" || field.DataType == "nvarchar" || field.DataType == "tinyint")
                    sw.AppendLine($"{field.FieldName.ToLower()} = jo.{field.FieldName},");
                else
                    sw.AppendLine($"{field.FieldName.ToLower()} = jo.{field.FieldName}.ConvertToDecimal(),");
            }
            return sw.ToString();
        }

        private static string GetReportInsertSql(ReportType reportType, List<string> ignoreFields)
        {
            var tableName = GetTableName(reportType);

            StringBuilder sw = new StringBuilder();

            var dataFieldList = GetTableDataFieldList(reportType);

            sw.Append("insert into " + tableName);

            StringBuilder sbFields = new StringBuilder();
            StringBuilder sbValues = new StringBuilder();
            foreach (var field in dataFieldList)
            {
                if (ignoreFields.Contains(field.FieldName.ToLower()))
                    continue;

                sbFields.AppendFormat(field.FieldName.ToLower() + ",");
                sbValues.AppendFormat("@" + field.FieldName.ToLower() + ",");
            }

            sw.Append(" (" + sbFields.ToString().TrimEnd(',') + ")");
            sw.Append(" values (" + sbValues.ToString().TrimEnd(',') + ")");

            return sw.ToString();
        }

        private static string GetReportUpdateSql(ReportType reportType, List<string> ignoreFields)
        {
            var tableName = GetTableName(reportType);
            StringBuilder sw = new StringBuilder();

            var dataFieldList = GetTableDataFieldList(reportType);

            sw.Append("update " + tableName + " set ");

            var sbFields = new StringBuilder();
            foreach (var field in dataFieldList)
            {
                if (ignoreFields.Contains(field.FieldName.ToLower()))
                    continue;

                sbFields.AppendFormat("{0}=@{0},", field.FieldName.ToLower());
            }

            sw.Append(" " + sbFields.ToString().TrimEnd(','));
            sw.Append(" where id=@id");
            return sw.ToString();
        }

        #endregion

        #region 生成实体类

        public static void BuildAllTableEntityCodes()
        {
            BuildTableEntityCode(ReportType.BalanceSheet);
            BuildTableEntityCode(ReportType.Income);
            BuildTableEntityCode(ReportType.Cashflow);
        }

        public static void BuildAllJsonObjectCodes()
        {
            BuildJsonObjectCode(ReportType.BalanceSheet);
            BuildJsonObjectCode(ReportType.Income);
            BuildJsonObjectCode(ReportType.Cashflow);
        }

        /// <summary>
        /// 生成单个表的JsonObject类
        /// </summary>
        private static void BuildJsonObjectCode(ReportType reportType)
        {
            var tableName = GetTableName(reportType);

            var dirName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files");
            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);

            var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files", tableName + "_jo.cs");
            if (File.Exists(fileName))
                File.WriteAllText(fileName, string.Empty);

            using (StreamWriter sw = new StreamWriter(fileName))
            {
                sw.WriteLine("using Newtonsoft.Json;");
                sw.WriteLine("using System;");
                sw.WriteLine("using System.Collections.Generic;");
                sw.WriteLine("namespace NiceShot.Core.Models.JsonObjects{");
                sw.WriteLine("[JsonObject]");
                sw.WriteLine($"public class {tableName}_jo{{");
                var dataFieldList = GetTableDataFieldList(reportType);
                foreach (var field in dataFieldList)
                {
                    var dataType = "string";

                    if (field.DataType == "varchar" || field.DataType == "nvarchar")
                        dataType = "string";
                    else if (field.DataType == "tinyint")
                        dataType = "int";
                    else if (field.DataType == "date")
                        dataType = "string";

                    sw.WriteLine($"public {dataType} {field.FieldName} {{get;set;}}");
                }
                sw.WriteLine("}}");
            }
        }

        /// <summary>
        /// 生成数据库表实体类
        /// </summary>
        private static void BuildTableEntityCode(ReportType reportType)
        {
            var tableName = GetTableName(reportType);

            var dirName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files");
            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);

            var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "files", tableName + ".cs");
            if (File.Exists(fileName))
                File.WriteAllText(fileName, string.Empty);

            using (StreamWriter sw = new StreamWriter(fileName))
            {
                sw.WriteLine("using Newtonsoft.Json;");
                sw.WriteLine("using System;");
                sw.WriteLine("using System.Collections.Generic;");
                sw.WriteLine("namespace NiceShot.Core.Models.Entities{");

                sw.WriteLine($"public class {tableName}{{");
                sw.WriteLine("public long id {get;set;}");

                var dataFieldList = GetTableDataFieldList(reportType);
                foreach (var field in dataFieldList)
                {
                    var dataType = "decimal?";
                    if (field.DataType == "varchar")
                        dataType = "string";
                    else if (field.DataType == "tinyint")
                        dataType = "int";
                    else if (field.DataType == "date")
                        dataType = "DateTime?";
                    sw.WriteLine($"public {dataType} {field.FieldName.ToLower()} {{get;set;}}");
                }
                sw.WriteLine("}}");
            }
        }

        #endregion

        #region 创建普通公司三张财务报表的数据库表

        public static void BuildTableSQL()
        {
            var sqlBal = GetTableSQL(ReportType.BalanceSheet);
            Logger.Info(sqlBal);
            var sqlIncome = GetTableSQL(ReportType.Income);
            Logger.Info(sqlIncome);
            var sqlCashflow = GetTableSQL(ReportType.Cashflow);
            Logger.Info(sqlCashflow);
        }

        private static string GetTableName(ReportType reportType)
        {
            var tableName = string.Empty;
            switch (reportType)
            {
                case ReportType.BalanceSheet:
                    tableName = "em_bal_common_v1";
                    break;
                case ReportType.Income:
                    tableName = "em_inc_common_v1";
                    break;
                case ReportType.Cashflow:
                    tableName = "em_cf_common_v1";
                    break;
            }
            return tableName;
        }

        private static string GetTableSQL(ReportType reportType)
        {
            var bal_df_list = GetTableDataFieldList(reportType);

            var tableName = GetTableName(reportType);

            var sbSQL = new StringBuilder();
            sbSQL.AppendLine("CREATE TABLE IF NOT EXISTS `" + tableName + "`(");
            sbSQL.AppendLine("`id` bigint UNSIGNED AUTO_INCREMENT,");

            foreach (var df in bal_df_list)
            {
                if (df.DataType == "varchar")
                    sbSQL.AppendLine("`" + df.FieldName + "` VARCHAR(10) NULL,");
                else if (df.DataType == "nvarchar")
                    sbSQL.AppendLine("`" + df.FieldName + "` VARCHAR(30) NULL,");
                else if (df.DataType == "date")
                    sbSQL.AppendLine("`" + df.FieldName + "` DATE,");
                else
                    sbSQL.AppendLine("`" + df.FieldName + "` DECIMAL(18,2) NULL,");
            }

            sbSQL.AppendLine(" PRIMARY KEY ( `id` )");
            sbSQL.AppendLine(")ENGINE=InnoDB DEFAULT CHARSET=utf8;");

            return sbSQL.ToString();
        }

        private static List<DataField> GetTableDataFieldList(ReportType reportType)
        {
            string dataStructsFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "em_fin_rep_struct.xlsx");

            using (var package = new ExcelPackage(new FileInfo(dataStructsFileName)))
            {
                string sheetName = UtilsHelper.GetWorkSheetName(reportType);
                ExcelWorksheet worksheet = package.Workbook.Worksheets[sheetName];

                int rowCount = worksheet.Dimension.Rows;
                int ColCount = worksheet.Dimension.Columns;
                var dataList = new List<DataField>();
                for (int row = 2; row <= rowCount; row++)
                {
                    var dataField = new DataField();

                    dataField.FieldName = worksheet.Cells[row, 1].Value.ToEmptyString();
                    dataField.DataType = worksheet.Cells[row, 2].Value.ToEmptyString();
                    dataField.CnFieldName = worksheet.Cells[row, 3].Value.ToEmptyString();

                    if (string.IsNullOrEmpty(dataField.FieldName))
                        break;
                    dataList.Add(dataField);
                }

                return dataList;
            }
        }

        #endregion


        #region 操作

        #region	em_bal_common_v1
        public static em_bal_common_v1 get_em_bal_common_v1_data(string ts_code, string date)
        {
            using (IDbConnection conn = new MySqlConnection(MySqlDbHelper.NICESHOTDB_CONN_STRING))
            {
                string sql = "select * from em_bal_common_v1 where secucode='" + ts_code + "' and report_date='" + date + "'";
                return conn.Query<em_bal_common_v1>(sql).FirstOrDefault();
            }
        }

        public static bool oper_em_bal_common_v1_data(em_bal_common_v1_jo jo, bool includeUpdate)
        {
            try
            {
                em_bal_common_v1 edit_entity = get_em_bal_common_v1_data(jo.secucode, jo.report_date);
                if (edit_entity != null)
                {
                    string sql = "update em_bal_common_v1 set  secucode=@secucode,security_code=@security_code,report_date=@report_date,notice_date=@notice_date,update_date=@update_date,accept_deposit_interbank=@accept_deposit_interbank,accounts_payable=@accounts_payable,accounts_rece=@accounts_rece,accrued_expense=@accrued_expense,advance_receivables=@advance_receivables,agent_trade_security=@agent_trade_security,agent_underwrite_security=@agent_underwrite_security,amortize_cost_finasset=@amortize_cost_finasset,amortize_cost_finliab=@amortize_cost_finliab,amortize_cost_ncfinasset=@amortize_cost_ncfinasset,amortize_cost_ncfinliab=@amortize_cost_ncfinliab,appoint_fvtpl_finasset=@appoint_fvtpl_finasset,appoint_fvtpl_finliab=@appoint_fvtpl_finliab,asset_balance=@asset_balance,asset_other=@asset_other,assign_cash_dividend=@assign_cash_dividend,available_sale_finasset=@available_sale_finasset,bond_payable=@bond_payable,borrow_fund=@borrow_fund,buy_resale_finasset=@buy_resale_finasset,capital_reserve=@capital_reserve,cip=@cip,consumptive_biological_asset=@consumptive_biological_asset,contract_asset=@contract_asset,contract_liab=@contract_liab,convert_diff=@convert_diff,creditor_invest=@creditor_invest,current_asset_balance=@current_asset_balance,current_asset_other=@current_asset_other,current_liab_balance=@current_liab_balance,current_liab_other=@current_liab_other,defer_income=@defer_income,defer_income_1year=@defer_income_1year,defer_tax_asset=@defer_tax_asset,defer_tax_liab=@defer_tax_liab,derive_finasset=@derive_finasset,derive_finliab=@derive_finliab,develop_expense=@develop_expense,div_holdsale_asset=@div_holdsale_asset,div_holdsale_liab=@div_holdsale_liab,dividend_payable=@dividend_payable,dividend_rece=@dividend_rece,equity_balance=@equity_balance,equity_other=@equity_other,export_refund_rece=@export_refund_rece,fee_commission_payable=@fee_commission_payable,fin_fund=@fin_fund,finance_rece=@finance_rece,fixed_asset=@fixed_asset,fixed_asset_disposal=@fixed_asset_disposal,fvtoci_finasset=@fvtoci_finasset,fvtoci_ncfinasset=@fvtoci_ncfinasset,fvtpl_finasset=@fvtpl_finasset,fvtpl_finliab=@fvtpl_finliab,general_risk_reserve=@general_risk_reserve,goodwill=@goodwill,hold_maturity_invest=@hold_maturity_invest,holdsale_asset=@holdsale_asset,holdsale_liab=@holdsale_liab,insurance_contract_reserve=@insurance_contract_reserve,intangible_asset=@intangible_asset,interest_payable=@interest_payable,interest_rece=@interest_rece,internal_payable=@internal_payable,internal_rece=@internal_rece,inventory=@inventory,invest_realestate=@invest_realestate,lease_liab=@lease_liab,lend_fund=@lend_fund,liab_balance=@liab_balance,liab_equity_balance=@liab_equity_balance,liab_equity_other=@liab_equity_other,liab_other=@liab_other,loan_advance=@loan_advance,loan_pbc=@loan_pbc,long_equity_invest=@long_equity_invest,long_loan=@long_loan,long_payable=@long_payable,long_prepaid_expense=@long_prepaid_expense,long_rece=@long_rece,long_staffsalary_payable=@long_staffsalary_payable,minority_equity=@minority_equity,monetaryfunds=@monetaryfunds,noncurrent_asset_1year=@noncurrent_asset_1year,noncurrent_asset_balance=@noncurrent_asset_balance,noncurrent_asset_other=@noncurrent_asset_other,noncurrent_liab_1year=@noncurrent_liab_1year,noncurrent_liab_balance=@noncurrent_liab_balance,noncurrent_liab_other=@noncurrent_liab_other,note_accounts_payable=@note_accounts_payable,note_accounts_rece=@note_accounts_rece,note_payable=@note_payable,note_rece=@note_rece,oil_gas_asset=@oil_gas_asset,other_compre_income=@other_compre_income,other_creditor_invest=@other_creditor_invest,other_current_asset=@other_current_asset,other_current_liab=@other_current_liab,other_equity_invest=@other_equity_invest,other_equity_other=@other_equity_other,other_equity_tool=@other_equity_tool,other_noncurrent_asset=@other_noncurrent_asset,other_noncurrent_finasset=@other_noncurrent_finasset,other_noncurrent_liab=@other_noncurrent_liab,other_payable=@other_payable,other_rece=@other_rece,parent_equity_balance=@parent_equity_balance,parent_equity_other=@parent_equity_other,perpetual_bond=@perpetual_bond,perpetual_bond_paybale=@perpetual_bond_paybale,predict_current_liab=@predict_current_liab,predict_liab=@predict_liab,preferred_shares=@preferred_shares,preferred_shares_paybale=@preferred_shares_paybale,premium_rece=@premium_rece,prepayment=@prepayment,productive_biology_asset=@productive_biology_asset,project_material=@project_material,rc_reserve_rece=@rc_reserve_rece,reinsure_payable=@reinsure_payable,reinsure_rece=@reinsure_rece,sell_repo_finasset=@sell_repo_finasset,settle_excess_reserve=@settle_excess_reserve,share_capital=@share_capital,short_bond_payable=@short_bond_payable,short_fin_payable=@short_fin_payable,short_loan=@short_loan,special_payable=@special_payable,special_reserve=@special_reserve,staff_salary_payable=@staff_salary_payable,subsidy_rece=@subsidy_rece,surplus_reserve=@surplus_reserve,tax_payable=@tax_payable,total_assets=@total_assets,total_current_assets=@total_current_assets,total_current_liab=@total_current_liab,total_equity=@total_equity,total_liab_equity=@total_liab_equity,total_liabilities=@total_liabilities,total_noncurrent_assets=@total_noncurrent_assets,total_noncurrent_liab=@total_noncurrent_liab,total_other_payable=@total_other_payable,total_other_rece=@total_other_rece,total_parent_equity=@total_parent_equity,trade_finasset=@trade_finasset,trade_finasset_notfvtpl=@trade_finasset_notfvtpl,trade_finliab=@trade_finliab,trade_finliab_notfvtpl=@trade_finliab_notfvtpl,treasury_shares=@treasury_shares,unassign_rpofit=@unassign_rpofit,unconfirm_invest_loss=@unconfirm_invest_loss,useright_asset=@useright_asset,opinion_type=@opinion_type where id=@id";

                    if (includeUpdate)
                    {
                        using (IDbConnection conn = new MySqlConnection(MySqlDbHelper.NICESHOTDB_CONN_STRING))
                        {
                            conn.Execute(sql, new
                            {
                                secucode = jo.secucode,
                                security_code = jo.security_code,
                                report_date = jo.report_date.ConvertToDate(),
                                notice_date = jo.notice_date.ConvertToDate(),
                                update_date = jo.update_date.ConvertToDate(),
                                accept_deposit_interbank = jo.accept_deposit_interbank.ConvertToDecimal(),
                                accounts_payable = jo.accounts_payable.ConvertToDecimal(),
                                accounts_rece = jo.accounts_rece.ConvertToDecimal(),
                                accrued_expense = jo.accrued_expense.ConvertToDecimal(),
                                advance_receivables = jo.advance_receivables.ConvertToDecimal(),
                                agent_trade_security = jo.agent_trade_security.ConvertToDecimal(),
                                agent_underwrite_security = jo.agent_underwrite_security.ConvertToDecimal(),
                                amortize_cost_finasset = jo.amortize_cost_finasset.ConvertToDecimal(),
                                amortize_cost_finliab = jo.amortize_cost_finliab.ConvertToDecimal(),
                                amortize_cost_ncfinasset = jo.amortize_cost_ncfinasset.ConvertToDecimal(),
                                amortize_cost_ncfinliab = jo.amortize_cost_ncfinliab.ConvertToDecimal(),
                                appoint_fvtpl_finasset = jo.appoint_fvtpl_finasset.ConvertToDecimal(),
                                appoint_fvtpl_finliab = jo.appoint_fvtpl_finliab.ConvertToDecimal(),
                                asset_balance = jo.asset_balance.ConvertToDecimal(),
                                asset_other = jo.asset_other.ConvertToDecimal(),
                                assign_cash_dividend = jo.assign_cash_dividend.ConvertToDecimal(),
                                available_sale_finasset = jo.available_sale_finasset.ConvertToDecimal(),
                                bond_payable = jo.bond_payable.ConvertToDecimal(),
                                borrow_fund = jo.borrow_fund.ConvertToDecimal(),
                                buy_resale_finasset = jo.buy_resale_finasset.ConvertToDecimal(),
                                capital_reserve = jo.capital_reserve.ConvertToDecimal(),
                                cip = jo.cip.ConvertToDecimal(),
                                consumptive_biological_asset = jo.consumptive_biological_asset.ConvertToDecimal(),
                                contract_asset = jo.contract_asset.ConvertToDecimal(),
                                contract_liab = jo.contract_liab.ConvertToDecimal(),
                                convert_diff = jo.convert_diff.ConvertToDecimal(),
                                creditor_invest = jo.creditor_invest.ConvertToDecimal(),
                                current_asset_balance = jo.current_asset_balance.ConvertToDecimal(),
                                current_asset_other = jo.current_asset_other.ConvertToDecimal(),
                                current_liab_balance = jo.current_liab_balance.ConvertToDecimal(),
                                current_liab_other = jo.current_liab_other.ConvertToDecimal(),
                                defer_income = jo.defer_income.ConvertToDecimal(),
                                defer_income_1year = jo.defer_income_1year.ConvertToDecimal(),
                                defer_tax_asset = jo.defer_tax_asset.ConvertToDecimal(),
                                defer_tax_liab = jo.defer_tax_liab.ConvertToDecimal(),
                                derive_finasset = jo.derive_finasset.ConvertToDecimal(),
                                derive_finliab = jo.derive_finliab.ConvertToDecimal(),
                                develop_expense = jo.develop_expense.ConvertToDecimal(),
                                div_holdsale_asset = jo.div_holdsale_asset.ConvertToDecimal(),
                                div_holdsale_liab = jo.div_holdsale_liab.ConvertToDecimal(),
                                dividend_payable = jo.dividend_payable.ConvertToDecimal(),
                                dividend_rece = jo.dividend_rece.ConvertToDecimal(),
                                equity_balance = jo.equity_balance.ConvertToDecimal(),
                                equity_other = jo.equity_other.ConvertToDecimal(),
                                export_refund_rece = jo.export_refund_rece.ConvertToDecimal(),
                                fee_commission_payable = jo.fee_commission_payable.ConvertToDecimal(),
                                fin_fund = jo.fin_fund.ConvertToDecimal(),
                                finance_rece = jo.finance_rece.ConvertToDecimal(),
                                fixed_asset = jo.fixed_asset.ConvertToDecimal(),
                                fixed_asset_disposal = jo.fixed_asset_disposal.ConvertToDecimal(),
                                fvtoci_finasset = jo.fvtoci_finasset.ConvertToDecimal(),
                                fvtoci_ncfinasset = jo.fvtoci_ncfinasset.ConvertToDecimal(),
                                fvtpl_finasset = jo.fvtpl_finasset.ConvertToDecimal(),
                                fvtpl_finliab = jo.fvtpl_finliab.ConvertToDecimal(),
                                general_risk_reserve = jo.general_risk_reserve.ConvertToDecimal(),
                                goodwill = jo.goodwill.ConvertToDecimal(),
                                hold_maturity_invest = jo.hold_maturity_invest.ConvertToDecimal(),
                                holdsale_asset = jo.holdsale_asset.ConvertToDecimal(),
                                holdsale_liab = jo.holdsale_liab.ConvertToDecimal(),
                                insurance_contract_reserve = jo.insurance_contract_reserve.ConvertToDecimal(),
                                intangible_asset = jo.intangible_asset.ConvertToDecimal(),
                                interest_payable = jo.interest_payable.ConvertToDecimal(),
                                interest_rece = jo.interest_rece.ConvertToDecimal(),
                                internal_payable = jo.internal_payable.ConvertToDecimal(),
                                internal_rece = jo.internal_rece.ConvertToDecimal(),
                                inventory = jo.inventory.ConvertToDecimal(),
                                invest_realestate = jo.invest_realestate.ConvertToDecimal(),
                                lease_liab = jo.lease_liab.ConvertToDecimal(),
                                lend_fund = jo.lend_fund.ConvertToDecimal(),
                                liab_balance = jo.liab_balance.ConvertToDecimal(),
                                liab_equity_balance = jo.liab_equity_balance.ConvertToDecimal(),
                                liab_equity_other = jo.liab_equity_other.ConvertToDecimal(),
                                liab_other = jo.liab_other.ConvertToDecimal(),
                                loan_advance = jo.loan_advance.ConvertToDecimal(),
                                loan_pbc = jo.loan_pbc.ConvertToDecimal(),
                                long_equity_invest = jo.long_equity_invest.ConvertToDecimal(),
                                long_loan = jo.long_loan.ConvertToDecimal(),
                                long_payable = jo.long_payable.ConvertToDecimal(),
                                long_prepaid_expense = jo.long_prepaid_expense.ConvertToDecimal(),
                                long_rece = jo.long_rece.ConvertToDecimal(),
                                long_staffsalary_payable = jo.long_staffsalary_payable.ConvertToDecimal(),
                                minority_equity = jo.minority_equity.ConvertToDecimal(),
                                monetaryfunds = jo.monetaryfunds.ConvertToDecimal(),
                                noncurrent_asset_1year = jo.noncurrent_asset_1year.ConvertToDecimal(),
                                noncurrent_asset_balance = jo.noncurrent_asset_balance.ConvertToDecimal(),
                                noncurrent_asset_other = jo.noncurrent_asset_other.ConvertToDecimal(),
                                noncurrent_liab_1year = jo.noncurrent_liab_1year.ConvertToDecimal(),
                                noncurrent_liab_balance = jo.noncurrent_liab_balance.ConvertToDecimal(),
                                noncurrent_liab_other = jo.noncurrent_liab_other.ConvertToDecimal(),
                                note_accounts_payable = jo.note_accounts_payable.ConvertToDecimal(),
                                note_accounts_rece = jo.note_accounts_rece.ConvertToDecimal(),
                                note_payable = jo.note_payable.ConvertToDecimal(),
                                note_rece = jo.note_rece.ConvertToDecimal(),
                                oil_gas_asset = jo.oil_gas_asset.ConvertToDecimal(),
                                other_compre_income = jo.other_compre_income.ConvertToDecimal(),
                                other_creditor_invest = jo.other_creditor_invest.ConvertToDecimal(),
                                other_current_asset = jo.other_current_asset.ConvertToDecimal(),
                                other_current_liab = jo.other_current_liab.ConvertToDecimal(),
                                other_equity_invest = jo.other_equity_invest.ConvertToDecimal(),
                                other_equity_other = jo.other_equity_other.ConvertToDecimal(),
                                other_equity_tool = jo.other_equity_tool.ConvertToDecimal(),
                                other_noncurrent_asset = jo.other_noncurrent_asset.ConvertToDecimal(),
                                other_noncurrent_finasset = jo.other_noncurrent_finasset.ConvertToDecimal(),
                                other_noncurrent_liab = jo.other_noncurrent_liab.ConvertToDecimal(),
                                other_payable = jo.other_payable.ConvertToDecimal(),
                                other_rece = jo.other_rece.ConvertToDecimal(),
                                parent_equity_balance = jo.parent_equity_balance.ConvertToDecimal(),
                                parent_equity_other = jo.parent_equity_other.ConvertToDecimal(),
                                perpetual_bond = jo.perpetual_bond.ConvertToDecimal(),
                                perpetual_bond_paybale = jo.perpetual_bond_paybale.ConvertToDecimal(),
                                predict_current_liab = jo.predict_current_liab.ConvertToDecimal(),
                                predict_liab = jo.predict_liab.ConvertToDecimal(),
                                preferred_shares = jo.preferred_shares.ConvertToDecimal(),
                                preferred_shares_paybale = jo.preferred_shares_paybale.ConvertToDecimal(),
                                premium_rece = jo.premium_rece.ConvertToDecimal(),
                                prepayment = jo.prepayment.ConvertToDecimal(),
                                productive_biology_asset = jo.productive_biology_asset.ConvertToDecimal(),
                                project_material = jo.project_material.ConvertToDecimal(),
                                rc_reserve_rece = jo.rc_reserve_rece.ConvertToDecimal(),
                                reinsure_payable = jo.reinsure_payable.ConvertToDecimal(),
                                reinsure_rece = jo.reinsure_rece.ConvertToDecimal(),
                                sell_repo_finasset = jo.sell_repo_finasset.ConvertToDecimal(),
                                settle_excess_reserve = jo.settle_excess_reserve.ConvertToDecimal(),
                                share_capital = jo.share_capital.ConvertToDecimal(),
                                short_bond_payable = jo.short_bond_payable.ConvertToDecimal(),
                                short_fin_payable = jo.short_fin_payable.ConvertToDecimal(),
                                short_loan = jo.short_loan.ConvertToDecimal(),
                                special_payable = jo.special_payable.ConvertToDecimal(),
                                special_reserve = jo.special_reserve.ConvertToDecimal(),
                                staff_salary_payable = jo.staff_salary_payable.ConvertToDecimal(),
                                subsidy_rece = jo.subsidy_rece.ConvertToDecimal(),
                                surplus_reserve = jo.surplus_reserve.ConvertToDecimal(),
                                tax_payable = jo.tax_payable.ConvertToDecimal(),
                                total_assets = jo.total_assets.ConvertToDecimal(),
                                total_current_assets = jo.total_current_assets.ConvertToDecimal(),
                                total_current_liab = jo.total_current_liab.ConvertToDecimal(),
                                total_equity = jo.total_equity.ConvertToDecimal(),
                                total_liab_equity = jo.total_liab_equity.ConvertToDecimal(),
                                total_liabilities = jo.total_liabilities.ConvertToDecimal(),
                                total_noncurrent_assets = jo.total_noncurrent_assets.ConvertToDecimal(),
                                total_noncurrent_liab = jo.total_noncurrent_liab.ConvertToDecimal(),
                                total_other_payable = jo.total_other_payable.ConvertToDecimal(),
                                total_other_rece = jo.total_other_rece.ConvertToDecimal(),
                                total_parent_equity = jo.total_parent_equity.ConvertToDecimal(),
                                trade_finasset = jo.trade_finasset.ConvertToDecimal(),
                                trade_finasset_notfvtpl = jo.trade_finasset_notfvtpl.ConvertToDecimal(),
                                trade_finliab = jo.trade_finliab.ConvertToDecimal(),
                                trade_finliab_notfvtpl = jo.trade_finliab_notfvtpl.ConvertToDecimal(),
                                treasury_shares = jo.treasury_shares.ConvertToDecimal(),
                                unassign_rpofit = jo.unassign_rpofit.ConvertToDecimal(),
                                unconfirm_invest_loss = jo.unconfirm_invest_loss.ConvertToDecimal(),
                                useright_asset = jo.useright_asset.ConvertToDecimal(),
                                opinion_type = StringUtils.FromUnicodeString(jo.opinion_type),

                                id = edit_entity.id
                            });
                        }
                        Logger.Info(string.Format("update data: tscode={0};date={1}", jo.secucode, jo.report_date));
                    }
                }
                else
                {
                    Logger.Info(string.Format("insert data: tscode={0};date={1}", jo.secucode, jo.report_date));
                    string sql = "insert into em_bal_common_v1 (secucode,security_code,report_date,notice_date,update_date,accept_deposit_interbank,accounts_payable,accounts_rece,accrued_expense,advance_receivables,agent_trade_security,agent_underwrite_security,amortize_cost_finasset,amortize_cost_finliab,amortize_cost_ncfinasset,amortize_cost_ncfinliab,appoint_fvtpl_finasset,appoint_fvtpl_finliab,asset_balance,asset_other,assign_cash_dividend,available_sale_finasset,bond_payable,borrow_fund,buy_resale_finasset,capital_reserve,cip,consumptive_biological_asset,contract_asset,contract_liab,convert_diff,creditor_invest,current_asset_balance,current_asset_other,current_liab_balance,current_liab_other,defer_income,defer_income_1year,defer_tax_asset,defer_tax_liab,derive_finasset,derive_finliab,develop_expense,div_holdsale_asset,div_holdsale_liab,dividend_payable,dividend_rece,equity_balance,equity_other,export_refund_rece,fee_commission_payable,fin_fund,finance_rece,fixed_asset,fixed_asset_disposal,fvtoci_finasset,fvtoci_ncfinasset,fvtpl_finasset,fvtpl_finliab,general_risk_reserve,goodwill,hold_maturity_invest,holdsale_asset,holdsale_liab,insurance_contract_reserve,intangible_asset,interest_payable,interest_rece,internal_payable,internal_rece,inventory,invest_realestate,lease_liab,lend_fund,liab_balance,liab_equity_balance,liab_equity_other,liab_other,loan_advance,loan_pbc,long_equity_invest,long_loan,long_payable,long_prepaid_expense,long_rece,long_staffsalary_payable,minority_equity,monetaryfunds,noncurrent_asset_1year,noncurrent_asset_balance,noncurrent_asset_other,noncurrent_liab_1year,noncurrent_liab_balance,noncurrent_liab_other,note_accounts_payable,note_accounts_rece,note_payable,note_rece,oil_gas_asset,other_compre_income,other_creditor_invest,other_current_asset,other_current_liab,other_equity_invest,other_equity_other,other_equity_tool,other_noncurrent_asset,other_noncurrent_finasset,other_noncurrent_liab,other_payable,other_rece,parent_equity_balance,parent_equity_other,perpetual_bond,perpetual_bond_paybale,predict_current_liab,predict_liab,preferred_shares,preferred_shares_paybale,premium_rece,prepayment,productive_biology_asset,project_material,rc_reserve_rece,reinsure_payable,reinsure_rece,sell_repo_finasset,settle_excess_reserve,share_capital,short_bond_payable,short_fin_payable,short_loan,special_payable,special_reserve,staff_salary_payable,subsidy_rece,surplus_reserve,tax_payable,total_assets,total_current_assets,total_current_liab,total_equity,total_liab_equity,total_liabilities,total_noncurrent_assets,total_noncurrent_liab,total_other_payable,total_other_rece,total_parent_equity,trade_finasset,trade_finasset_notfvtpl,trade_finliab,trade_finliab_notfvtpl,treasury_shares,unassign_rpofit,unconfirm_invest_loss,useright_asset,opinion_type) values (@secucode,@security_code,@report_date,@notice_date,@update_date,@accept_deposit_interbank,@accounts_payable,@accounts_rece,@accrued_expense,@advance_receivables,@agent_trade_security,@agent_underwrite_security,@amortize_cost_finasset,@amortize_cost_finliab,@amortize_cost_ncfinasset,@amortize_cost_ncfinliab,@appoint_fvtpl_finasset,@appoint_fvtpl_finliab,@asset_balance,@asset_other,@assign_cash_dividend,@available_sale_finasset,@bond_payable,@borrow_fund,@buy_resale_finasset,@capital_reserve,@cip,@consumptive_biological_asset,@contract_asset,@contract_liab,@convert_diff,@creditor_invest,@current_asset_balance,@current_asset_other,@current_liab_balance,@current_liab_other,@defer_income,@defer_income_1year,@defer_tax_asset,@defer_tax_liab,@derive_finasset,@derive_finliab,@develop_expense,@div_holdsale_asset,@div_holdsale_liab,@dividend_payable,@dividend_rece,@equity_balance,@equity_other,@export_refund_rece,@fee_commission_payable,@fin_fund,@finance_rece,@fixed_asset,@fixed_asset_disposal,@fvtoci_finasset,@fvtoci_ncfinasset,@fvtpl_finasset,@fvtpl_finliab,@general_risk_reserve,@goodwill,@hold_maturity_invest,@holdsale_asset,@holdsale_liab,@insurance_contract_reserve,@intangible_asset,@interest_payable,@interest_rece,@internal_payable,@internal_rece,@inventory,@invest_realestate,@lease_liab,@lend_fund,@liab_balance,@liab_equity_balance,@liab_equity_other,@liab_other,@loan_advance,@loan_pbc,@long_equity_invest,@long_loan,@long_payable,@long_prepaid_expense,@long_rece,@long_staffsalary_payable,@minority_equity,@monetaryfunds,@noncurrent_asset_1year,@noncurrent_asset_balance,@noncurrent_asset_other,@noncurrent_liab_1year,@noncurrent_liab_balance,@noncurrent_liab_other,@note_accounts_payable,@note_accounts_rece,@note_payable,@note_rece,@oil_gas_asset,@other_compre_income,@other_creditor_invest,@other_current_asset,@other_current_liab,@other_equity_invest,@other_equity_other,@other_equity_tool,@other_noncurrent_asset,@other_noncurrent_finasset,@other_noncurrent_liab,@other_payable,@other_rece,@parent_equity_balance,@parent_equity_other,@perpetual_bond,@perpetual_bond_paybale,@predict_current_liab,@predict_liab,@preferred_shares,@preferred_shares_paybale,@premium_rece,@prepayment,@productive_biology_asset,@project_material,@rc_reserve_rece,@reinsure_payable,@reinsure_rece,@sell_repo_finasset,@settle_excess_reserve,@share_capital,@short_bond_payable,@short_fin_payable,@short_loan,@special_payable,@special_reserve,@staff_salary_payable,@subsidy_rece,@surplus_reserve,@tax_payable,@total_assets,@total_current_assets,@total_current_liab,@total_equity,@total_liab_equity,@total_liabilities,@total_noncurrent_assets,@total_noncurrent_liab,@total_other_payable,@total_other_rece,@total_parent_equity,@trade_finasset,@trade_finasset_notfvtpl,@trade_finliab,@trade_finliab_notfvtpl,@treasury_shares,@unassign_rpofit,@unconfirm_invest_loss,@useright_asset,@opinion_type)";
                    em_bal_common_v1 entity = new em_bal_common_v1()
                    {
                        secucode = jo.secucode,
                        security_code = jo.security_code,
                        report_date = jo.report_date.ConvertToDate(),
                        notice_date = jo.notice_date.ConvertToDate(),
                        update_date = jo.update_date.ConvertToDate(),
                        accept_deposit_interbank = jo.accept_deposit_interbank.ConvertToDecimal(),
                        accounts_payable = jo.accounts_payable.ConvertToDecimal(),
                        accounts_rece = jo.accounts_rece.ConvertToDecimal(),
                        accrued_expense = jo.accrued_expense.ConvertToDecimal(),
                        advance_receivables = jo.advance_receivables.ConvertToDecimal(),
                        agent_trade_security = jo.agent_trade_security.ConvertToDecimal(),
                        agent_underwrite_security = jo.agent_underwrite_security.ConvertToDecimal(),
                        amortize_cost_finasset = jo.amortize_cost_finasset.ConvertToDecimal(),
                        amortize_cost_finliab = jo.amortize_cost_finliab.ConvertToDecimal(),
                        amortize_cost_ncfinasset = jo.amortize_cost_ncfinasset.ConvertToDecimal(),
                        amortize_cost_ncfinliab = jo.amortize_cost_ncfinliab.ConvertToDecimal(),
                        appoint_fvtpl_finasset = jo.appoint_fvtpl_finasset.ConvertToDecimal(),
                        appoint_fvtpl_finliab = jo.appoint_fvtpl_finliab.ConvertToDecimal(),
                        asset_balance = jo.asset_balance.ConvertToDecimal(),
                        asset_other = jo.asset_other.ConvertToDecimal(),
                        assign_cash_dividend = jo.assign_cash_dividend.ConvertToDecimal(),
                        available_sale_finasset = jo.available_sale_finasset.ConvertToDecimal(),
                        bond_payable = jo.bond_payable.ConvertToDecimal(),
                        borrow_fund = jo.borrow_fund.ConvertToDecimal(),
                        buy_resale_finasset = jo.buy_resale_finasset.ConvertToDecimal(),
                        capital_reserve = jo.capital_reserve.ConvertToDecimal(),
                        cip = jo.cip.ConvertToDecimal(),
                        consumptive_biological_asset = jo.consumptive_biological_asset.ConvertToDecimal(),
                        contract_asset = jo.contract_asset.ConvertToDecimal(),
                        contract_liab = jo.contract_liab.ConvertToDecimal(),
                        convert_diff = jo.convert_diff.ConvertToDecimal(),
                        creditor_invest = jo.creditor_invest.ConvertToDecimal(),
                        current_asset_balance = jo.current_asset_balance.ConvertToDecimal(),
                        current_asset_other = jo.current_asset_other.ConvertToDecimal(),
                        current_liab_balance = jo.current_liab_balance.ConvertToDecimal(),
                        current_liab_other = jo.current_liab_other.ConvertToDecimal(),
                        defer_income = jo.defer_income.ConvertToDecimal(),
                        defer_income_1year = jo.defer_income_1year.ConvertToDecimal(),
                        defer_tax_asset = jo.defer_tax_asset.ConvertToDecimal(),
                        defer_tax_liab = jo.defer_tax_liab.ConvertToDecimal(),
                        derive_finasset = jo.derive_finasset.ConvertToDecimal(),
                        derive_finliab = jo.derive_finliab.ConvertToDecimal(),
                        develop_expense = jo.develop_expense.ConvertToDecimal(),
                        div_holdsale_asset = jo.div_holdsale_asset.ConvertToDecimal(),
                        div_holdsale_liab = jo.div_holdsale_liab.ConvertToDecimal(),
                        dividend_payable = jo.dividend_payable.ConvertToDecimal(),
                        dividend_rece = jo.dividend_rece.ConvertToDecimal(),
                        equity_balance = jo.equity_balance.ConvertToDecimal(),
                        equity_other = jo.equity_other.ConvertToDecimal(),
                        export_refund_rece = jo.export_refund_rece.ConvertToDecimal(),
                        fee_commission_payable = jo.fee_commission_payable.ConvertToDecimal(),
                        fin_fund = jo.fin_fund.ConvertToDecimal(),
                        finance_rece = jo.finance_rece.ConvertToDecimal(),
                        fixed_asset = jo.fixed_asset.ConvertToDecimal(),
                        fixed_asset_disposal = jo.fixed_asset_disposal.ConvertToDecimal(),
                        fvtoci_finasset = jo.fvtoci_finasset.ConvertToDecimal(),
                        fvtoci_ncfinasset = jo.fvtoci_ncfinasset.ConvertToDecimal(),
                        fvtpl_finasset = jo.fvtpl_finasset.ConvertToDecimal(),
                        fvtpl_finliab = jo.fvtpl_finliab.ConvertToDecimal(),
                        general_risk_reserve = jo.general_risk_reserve.ConvertToDecimal(),
                        goodwill = jo.goodwill.ConvertToDecimal(),
                        hold_maturity_invest = jo.hold_maturity_invest.ConvertToDecimal(),
                        holdsale_asset = jo.holdsale_asset.ConvertToDecimal(),
                        holdsale_liab = jo.holdsale_liab.ConvertToDecimal(),
                        insurance_contract_reserve = jo.insurance_contract_reserve.ConvertToDecimal(),
                        intangible_asset = jo.intangible_asset.ConvertToDecimal(),
                        interest_payable = jo.interest_payable.ConvertToDecimal(),
                        interest_rece = jo.interest_rece.ConvertToDecimal(),
                        internal_payable = jo.internal_payable.ConvertToDecimal(),
                        internal_rece = jo.internal_rece.ConvertToDecimal(),
                        inventory = jo.inventory.ConvertToDecimal(),
                        invest_realestate = jo.invest_realestate.ConvertToDecimal(),
                        lease_liab = jo.lease_liab.ConvertToDecimal(),
                        lend_fund = jo.lend_fund.ConvertToDecimal(),
                        liab_balance = jo.liab_balance.ConvertToDecimal(),
                        liab_equity_balance = jo.liab_equity_balance.ConvertToDecimal(),
                        liab_equity_other = jo.liab_equity_other.ConvertToDecimal(),
                        liab_other = jo.liab_other.ConvertToDecimal(),
                        loan_advance = jo.loan_advance.ConvertToDecimal(),
                        loan_pbc = jo.loan_pbc.ConvertToDecimal(),
                        long_equity_invest = jo.long_equity_invest.ConvertToDecimal(),
                        long_loan = jo.long_loan.ConvertToDecimal(),
                        long_payable = jo.long_payable.ConvertToDecimal(),
                        long_prepaid_expense = jo.long_prepaid_expense.ConvertToDecimal(),
                        long_rece = jo.long_rece.ConvertToDecimal(),
                        long_staffsalary_payable = jo.long_staffsalary_payable.ConvertToDecimal(),
                        minority_equity = jo.minority_equity.ConvertToDecimal(),
                        monetaryfunds = jo.monetaryfunds.ConvertToDecimal(),
                        noncurrent_asset_1year = jo.noncurrent_asset_1year.ConvertToDecimal(),
                        noncurrent_asset_balance = jo.noncurrent_asset_balance.ConvertToDecimal(),
                        noncurrent_asset_other = jo.noncurrent_asset_other.ConvertToDecimal(),
                        noncurrent_liab_1year = jo.noncurrent_liab_1year.ConvertToDecimal(),
                        noncurrent_liab_balance = jo.noncurrent_liab_balance.ConvertToDecimal(),
                        noncurrent_liab_other = jo.noncurrent_liab_other.ConvertToDecimal(),
                        note_accounts_payable = jo.note_accounts_payable.ConvertToDecimal(),
                        note_accounts_rece = jo.note_accounts_rece.ConvertToDecimal(),
                        note_payable = jo.note_payable.ConvertToDecimal(),
                        note_rece = jo.note_rece.ConvertToDecimal(),
                        oil_gas_asset = jo.oil_gas_asset.ConvertToDecimal(),
                        other_compre_income = jo.other_compre_income.ConvertToDecimal(),
                        other_creditor_invest = jo.other_creditor_invest.ConvertToDecimal(),
                        other_current_asset = jo.other_current_asset.ConvertToDecimal(),
                        other_current_liab = jo.other_current_liab.ConvertToDecimal(),
                        other_equity_invest = jo.other_equity_invest.ConvertToDecimal(),
                        other_equity_other = jo.other_equity_other.ConvertToDecimal(),
                        other_equity_tool = jo.other_equity_tool.ConvertToDecimal(),
                        other_noncurrent_asset = jo.other_noncurrent_asset.ConvertToDecimal(),
                        other_noncurrent_finasset = jo.other_noncurrent_finasset.ConvertToDecimal(),
                        other_noncurrent_liab = jo.other_noncurrent_liab.ConvertToDecimal(),
                        other_payable = jo.other_payable.ConvertToDecimal(),
                        other_rece = jo.other_rece.ConvertToDecimal(),
                        parent_equity_balance = jo.parent_equity_balance.ConvertToDecimal(),
                        parent_equity_other = jo.parent_equity_other.ConvertToDecimal(),
                        perpetual_bond = jo.perpetual_bond.ConvertToDecimal(),
                        perpetual_bond_paybale = jo.perpetual_bond_paybale.ConvertToDecimal(),
                        predict_current_liab = jo.predict_current_liab.ConvertToDecimal(),
                        predict_liab = jo.predict_liab.ConvertToDecimal(),
                        preferred_shares = jo.preferred_shares.ConvertToDecimal(),
                        preferred_shares_paybale = jo.preferred_shares_paybale.ConvertToDecimal(),
                        premium_rece = jo.premium_rece.ConvertToDecimal(),
                        prepayment = jo.prepayment.ConvertToDecimal(),
                        productive_biology_asset = jo.productive_biology_asset.ConvertToDecimal(),
                        project_material = jo.project_material.ConvertToDecimal(),
                        rc_reserve_rece = jo.rc_reserve_rece.ConvertToDecimal(),
                        reinsure_payable = jo.reinsure_payable.ConvertToDecimal(),
                        reinsure_rece = jo.reinsure_rece.ConvertToDecimal(),
                        sell_repo_finasset = jo.sell_repo_finasset.ConvertToDecimal(),
                        settle_excess_reserve = jo.settle_excess_reserve.ConvertToDecimal(),
                        share_capital = jo.share_capital.ConvertToDecimal(),
                        short_bond_payable = jo.short_bond_payable.ConvertToDecimal(),
                        short_fin_payable = jo.short_fin_payable.ConvertToDecimal(),
                        short_loan = jo.short_loan.ConvertToDecimal(),
                        special_payable = jo.special_payable.ConvertToDecimal(),
                        special_reserve = jo.special_reserve.ConvertToDecimal(),
                        staff_salary_payable = jo.staff_salary_payable.ConvertToDecimal(),
                        subsidy_rece = jo.subsidy_rece.ConvertToDecimal(),
                        surplus_reserve = jo.surplus_reserve.ConvertToDecimal(),
                        tax_payable = jo.tax_payable.ConvertToDecimal(),
                        total_assets = jo.total_assets.ConvertToDecimal(),
                        total_current_assets = jo.total_current_assets.ConvertToDecimal(),
                        total_current_liab = jo.total_current_liab.ConvertToDecimal(),
                        total_equity = jo.total_equity.ConvertToDecimal(),
                        total_liab_equity = jo.total_liab_equity.ConvertToDecimal(),
                        total_liabilities = jo.total_liabilities.ConvertToDecimal(),
                        total_noncurrent_assets = jo.total_noncurrent_assets.ConvertToDecimal(),
                        total_noncurrent_liab = jo.total_noncurrent_liab.ConvertToDecimal(),
                        total_other_payable = jo.total_other_payable.ConvertToDecimal(),
                        total_other_rece = jo.total_other_rece.ConvertToDecimal(),
                        total_parent_equity = jo.total_parent_equity.ConvertToDecimal(),
                        trade_finasset = jo.trade_finasset.ConvertToDecimal(),
                        trade_finasset_notfvtpl = jo.trade_finasset_notfvtpl.ConvertToDecimal(),
                        trade_finliab = jo.trade_finliab.ConvertToDecimal(),
                        trade_finliab_notfvtpl = jo.trade_finliab_notfvtpl.ConvertToDecimal(),
                        treasury_shares = jo.treasury_shares.ConvertToDecimal(),
                        unassign_rpofit = jo.unassign_rpofit.ConvertToDecimal(),
                        unconfirm_invest_loss = jo.unconfirm_invest_loss.ConvertToDecimal(),
                        useright_asset = jo.useright_asset.ConvertToDecimal(),
                        opinion_type = StringUtils.FromUnicodeString(jo.opinion_type),

                    };
                    using (IDbConnection conn = new MySqlConnection(MySqlDbHelper.NICESHOTDB_CONN_STRING))
                    {
                        conn.Execute(sql, entity);
                    }
                }

                return edit_entity != null;
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("sync em report data occurs error: tscode={0};date={1},details:{2}", jo.secucode, jo.report_date, ex));
            }

            return false;
        }
        #endregion

        #region	em_inc_common_v1
        public static em_inc_common_v1 get_em_inc_common_v1_data(string ts_code, string date)
        {
            using (IDbConnection conn = new MySqlConnection(MySqlDbHelper.NICESHOTDB_CONN_STRING))
            {
                string sql = "select * from em_inc_common_v1 where secucode='" + ts_code + "' and report_date='" + date + "'";
                return conn.Query<em_inc_common_v1>(sql).FirstOrDefault();
            }
        }

        public static bool oper_em_inc_common_v1_data(em_inc_common_v1_jo jo, bool includeUpdate)
        {
            try
            {
                em_inc_common_v1 edit_entity = get_em_inc_common_v1_data(jo.secucode, jo.report_date);
                if (edit_entity != null)
                {
                    string sql = "update em_inc_common_v1 set  secucode=@secucode,security_code=@security_code,report_date=@report_date,notice_date=@notice_date,update_date=@update_date,total_operate_income=@total_operate_income,operate_income=@operate_income,interest_income=@interest_income,earned_premium=@earned_premium,fee_commission_income=@fee_commission_income,other_business_income=@other_business_income,toi_other=@toi_other,total_operate_cost=@total_operate_cost,operate_cost=@operate_cost,interest_expense=@interest_expense,fee_commission_expense=@fee_commission_expense,research_expense=@research_expense,surrender_value=@surrender_value,net_compensate_expense=@net_compensate_expense,net_contract_reserve=@net_contract_reserve,policy_bonus_expense=@policy_bonus_expense,reinsure_expense=@reinsure_expense,other_business_cost=@other_business_cost,operate_tax_add=@operate_tax_add,sale_expense=@sale_expense,manage_expense=@manage_expense,me_research_expense=@me_research_expense,finance_expense=@finance_expense,fe_interest_expense=@fe_interest_expense,fe_interest_income=@fe_interest_income,asset_impairment_loss=@asset_impairment_loss,credit_impairment_loss=@credit_impairment_loss,toc_other=@toc_other,fairvalue_change_income=@fairvalue_change_income,invest_income=@invest_income,invest_joint_income=@invest_joint_income,net_exposure_income=@net_exposure_income,exchange_income=@exchange_income,asset_disposal_income=@asset_disposal_income,asset_impairment_income=@asset_impairment_income,credit_impairment_income=@credit_impairment_income,other_income=@other_income,operate_profit_other=@operate_profit_other,operate_profit_balance=@operate_profit_balance,operate_profit=@operate_profit,nonbusiness_income=@nonbusiness_income,noncurrent_disposal_income=@noncurrent_disposal_income,nonbusiness_expense=@nonbusiness_expense,noncurrent_disposal_loss=@noncurrent_disposal_loss,effect_tp_other=@effect_tp_other,total_profit_balance=@total_profit_balance,total_profit=@total_profit,income_tax=@income_tax,effect_netprofit_other=@effect_netprofit_other,effect_netprofit_balance=@effect_netprofit_balance,unconfirm_invest_loss=@unconfirm_invest_loss,netprofit=@netprofit,precombine_profit=@precombine_profit,continued_netprofit=@continued_netprofit,discontinued_netprofit=@discontinued_netprofit,parent_netprofit=@parent_netprofit,minority_interest=@minority_interest,deduct_parent_netprofit=@deduct_parent_netprofit,netprofit_other=@netprofit_other,netprofit_balance=@netprofit_balance,basic_eps=@basic_eps,diluted_eps=@diluted_eps,other_compre_income=@other_compre_income,parent_oci=@parent_oci,minority_oci=@minority_oci,parent_oci_other=@parent_oci_other,parent_oci_balance=@parent_oci_balance,unable_oci=@unable_oci,creditrisk_fairvalue_change=@creditrisk_fairvalue_change,otherright_fairvalue_change=@otherright_fairvalue_change,setup_profit_change=@setup_profit_change,rightlaw_unable_oci=@rightlaw_unable_oci,unable_oci_other=@unable_oci_other,unable_oci_balance=@unable_oci_balance,able_oci=@able_oci,rightlaw_able_oci=@rightlaw_able_oci,afa_fairvalue_change=@afa_fairvalue_change,hmi_afa=@hmi_afa,cashflow_hedge_valid=@cashflow_hedge_valid,creditor_fairvalue_change=@creditor_fairvalue_change,creditor_impairment_reserve=@creditor_impairment_reserve,finance_oci_amt=@finance_oci_amt,convert_diff=@convert_diff,able_oci_other=@able_oci_other,able_oci_balance=@able_oci_balance,oci_other=@oci_other,oci_balance=@oci_balance,total_compre_income=@total_compre_income,parent_tci=@parent_tci,minority_tci=@minority_tci,precombine_tci=@precombine_tci,effect_tci_balance=@effect_tci_balance,tci_other=@tci_other,tci_balance=@tci_balance,acf_end_income=@acf_end_income,opinion_type=@opinion_type where id=@id";
                    if (includeUpdate)
                    {
                        using (IDbConnection conn = new MySqlConnection(MySqlDbHelper.NICESHOTDB_CONN_STRING))
                        {
                            conn.Execute(sql, new
                            {
                                secucode = jo.secucode,
                                security_code = jo.security_code,
                                report_date = jo.report_date.ConvertToDate(),
                                notice_date = jo.notice_date.ConvertToDate(),
                                update_date = jo.update_date.ConvertToDate(),
                                total_operate_income = jo.total_operate_income.ConvertToDecimal(),
                                operate_income = jo.operate_income.ConvertToDecimal(),
                                interest_income = jo.interest_income.ConvertToDecimal(),
                                earned_premium = jo.earned_premium.ConvertToDecimal(),
                                fee_commission_income = jo.fee_commission_income.ConvertToDecimal(),
                                other_business_income = jo.other_business_income.ConvertToDecimal(),
                                toi_other = jo.toi_other.ConvertToDecimal(),
                                total_operate_cost = jo.total_operate_cost.ConvertToDecimal(),
                                operate_cost = jo.operate_cost.ConvertToDecimal(),
                                interest_expense = jo.interest_expense.ConvertToDecimal(),
                                fee_commission_expense = jo.fee_commission_expense.ConvertToDecimal(),
                                research_expense = jo.research_expense.ConvertToDecimal(),
                                surrender_value = jo.surrender_value.ConvertToDecimal(),
                                net_compensate_expense = jo.net_compensate_expense.ConvertToDecimal(),
                                net_contract_reserve = jo.net_contract_reserve.ConvertToDecimal(),
                                policy_bonus_expense = jo.policy_bonus_expense.ConvertToDecimal(),
                                reinsure_expense = jo.reinsure_expense.ConvertToDecimal(),
                                other_business_cost = jo.other_business_cost.ConvertToDecimal(),
                                operate_tax_add = jo.operate_tax_add.ConvertToDecimal(),
                                sale_expense = jo.sale_expense.ConvertToDecimal(),
                                manage_expense = jo.manage_expense.ConvertToDecimal(),
                                me_research_expense = jo.me_research_expense.ConvertToDecimal(),
                                finance_expense = jo.finance_expense.ConvertToDecimal(),
                                fe_interest_expense = jo.fe_interest_expense.ConvertToDecimal(),
                                fe_interest_income = jo.fe_interest_income.ConvertToDecimal(),
                                asset_impairment_loss = jo.asset_impairment_loss.ConvertToDecimal(),
                                credit_impairment_loss = jo.credit_impairment_loss.ConvertToDecimal(),
                                toc_other = jo.toc_other.ConvertToDecimal(),
                                fairvalue_change_income = jo.fairvalue_change_income.ConvertToDecimal(),
                                invest_income = jo.invest_income.ConvertToDecimal(),
                                invest_joint_income = jo.invest_joint_income.ConvertToDecimal(),
                                net_exposure_income = jo.net_exposure_income.ConvertToDecimal(),
                                exchange_income = jo.exchange_income.ConvertToDecimal(),
                                asset_disposal_income = jo.asset_disposal_income.ConvertToDecimal(),
                                asset_impairment_income = jo.asset_impairment_income.ConvertToDecimal(),
                                credit_impairment_income = jo.credit_impairment_income.ConvertToDecimal(),
                                other_income = jo.other_income.ConvertToDecimal(),
                                operate_profit_other = jo.operate_profit_other.ConvertToDecimal(),
                                operate_profit_balance = jo.operate_profit_balance.ConvertToDecimal(),
                                operate_profit = jo.operate_profit.ConvertToDecimal(),
                                nonbusiness_income = jo.nonbusiness_income.ConvertToDecimal(),
                                noncurrent_disposal_income = jo.noncurrent_disposal_income.ConvertToDecimal(),
                                nonbusiness_expense = jo.nonbusiness_expense.ConvertToDecimal(),
                                noncurrent_disposal_loss = jo.noncurrent_disposal_loss.ConvertToDecimal(),
                                effect_tp_other = jo.effect_tp_other.ConvertToDecimal(),
                                total_profit_balance = jo.total_profit_balance.ConvertToDecimal(),
                                total_profit = jo.total_profit.ConvertToDecimal(),
                                income_tax = jo.income_tax.ConvertToDecimal(),
                                effect_netprofit_other = jo.effect_netprofit_other.ConvertToDecimal(),
                                effect_netprofit_balance = jo.effect_netprofit_balance.ConvertToDecimal(),
                                unconfirm_invest_loss = jo.unconfirm_invest_loss.ConvertToDecimal(),
                                netprofit = jo.netprofit.ConvertToDecimal(),
                                precombine_profit = jo.precombine_profit.ConvertToDecimal(),
                                continued_netprofit = jo.continued_netprofit.ConvertToDecimal(),
                                discontinued_netprofit = jo.discontinued_netprofit.ConvertToDecimal(),
                                parent_netprofit = jo.parent_netprofit.ConvertToDecimal(),
                                minority_interest = jo.minority_interest.ConvertToDecimal(),
                                deduct_parent_netprofit = jo.deduct_parent_netprofit.ConvertToDecimal(),
                                netprofit_other = jo.netprofit_other.ConvertToDecimal(),
                                netprofit_balance = jo.netprofit_balance.ConvertToDecimal(),
                                basic_eps = jo.basic_eps.ConvertToDecimal(),
                                diluted_eps = jo.diluted_eps.ConvertToDecimal(),
                                other_compre_income = jo.other_compre_income.ConvertToDecimal(),
                                parent_oci = jo.parent_oci.ConvertToDecimal(),
                                minority_oci = jo.minority_oci.ConvertToDecimal(),
                                parent_oci_other = jo.parent_oci_other.ConvertToDecimal(),
                                parent_oci_balance = jo.parent_oci_balance.ConvertToDecimal(),
                                unable_oci = jo.unable_oci.ConvertToDecimal(),
                                creditrisk_fairvalue_change = jo.creditrisk_fairvalue_change.ConvertToDecimal(),
                                otherright_fairvalue_change = jo.otherright_fairvalue_change.ConvertToDecimal(),
                                setup_profit_change = jo.setup_profit_change.ConvertToDecimal(),
                                rightlaw_unable_oci = jo.rightlaw_unable_oci.ConvertToDecimal(),
                                unable_oci_other = jo.unable_oci_other.ConvertToDecimal(),
                                unable_oci_balance = jo.unable_oci_balance.ConvertToDecimal(),
                                able_oci = jo.able_oci.ConvertToDecimal(),
                                rightlaw_able_oci = jo.rightlaw_able_oci.ConvertToDecimal(),
                                afa_fairvalue_change = jo.afa_fairvalue_change.ConvertToDecimal(),
                                hmi_afa = jo.hmi_afa.ConvertToDecimal(),
                                cashflow_hedge_valid = jo.cashflow_hedge_valid.ConvertToDecimal(),
                                creditor_fairvalue_change = jo.creditor_fairvalue_change.ConvertToDecimal(),
                                creditor_impairment_reserve = jo.creditor_impairment_reserve.ConvertToDecimal(),
                                finance_oci_amt = jo.finance_oci_amt.ConvertToDecimal(),
                                convert_diff = jo.convert_diff.ConvertToDecimal(),
                                able_oci_other = jo.able_oci_other.ConvertToDecimal(),
                                able_oci_balance = jo.able_oci_balance.ConvertToDecimal(),
                                oci_other = jo.oci_other.ConvertToDecimal(),
                                oci_balance = jo.oci_balance.ConvertToDecimal(),
                                total_compre_income = jo.total_compre_income.ConvertToDecimal(),
                                parent_tci = jo.parent_tci.ConvertToDecimal(),
                                minority_tci = jo.minority_tci.ConvertToDecimal(),
                                precombine_tci = jo.precombine_tci.ConvertToDecimal(),
                                effect_tci_balance = jo.effect_tci_balance.ConvertToDecimal(),
                                tci_other = jo.tci_other.ConvertToDecimal(),
                                tci_balance = jo.tci_balance.ConvertToDecimal(),
                                acf_end_income = jo.acf_end_income.ConvertToDecimal(),
                                opinion_type = StringUtils.FromUnicodeString(jo.opinion_type),

                                id = edit_entity.id
                            });
                        }
                        Logger.Info(string.Format("update data: tscode={0};date={1}", jo.secucode, jo.report_date));
                    }
                }
                else
                {
                    Logger.Info(string.Format("insert data: tscode={0};date={1}", jo.secucode, jo.report_date));
                    string sql = "insert into em_inc_common_v1 (secucode,security_code,report_date,notice_date,update_date,total_operate_income,operate_income,interest_income,earned_premium,fee_commission_income,other_business_income,toi_other,total_operate_cost,operate_cost,interest_expense,fee_commission_expense,research_expense,surrender_value,net_compensate_expense,net_contract_reserve,policy_bonus_expense,reinsure_expense,other_business_cost,operate_tax_add,sale_expense,manage_expense,me_research_expense,finance_expense,fe_interest_expense,fe_interest_income,asset_impairment_loss,credit_impairment_loss,toc_other,fairvalue_change_income,invest_income,invest_joint_income,net_exposure_income,exchange_income,asset_disposal_income,asset_impairment_income,credit_impairment_income,other_income,operate_profit_other,operate_profit_balance,operate_profit,nonbusiness_income,noncurrent_disposal_income,nonbusiness_expense,noncurrent_disposal_loss,effect_tp_other,total_profit_balance,total_profit,income_tax,effect_netprofit_other,effect_netprofit_balance,unconfirm_invest_loss,netprofit,precombine_profit,continued_netprofit,discontinued_netprofit,parent_netprofit,minority_interest,deduct_parent_netprofit,netprofit_other,netprofit_balance,basic_eps,diluted_eps,other_compre_income,parent_oci,minority_oci,parent_oci_other,parent_oci_balance,unable_oci,creditrisk_fairvalue_change,otherright_fairvalue_change,setup_profit_change,rightlaw_unable_oci,unable_oci_other,unable_oci_balance,able_oci,rightlaw_able_oci,afa_fairvalue_change,hmi_afa,cashflow_hedge_valid,creditor_fairvalue_change,creditor_impairment_reserve,finance_oci_amt,convert_diff,able_oci_other,able_oci_balance,oci_other,oci_balance,total_compre_income,parent_tci,minority_tci,precombine_tci,effect_tci_balance,tci_other,tci_balance,acf_end_income,opinion_type) values (@secucode,@security_code,@report_date,@notice_date,@update_date,@total_operate_income,@operate_income,@interest_income,@earned_premium,@fee_commission_income,@other_business_income,@toi_other,@total_operate_cost,@operate_cost,@interest_expense,@fee_commission_expense,@research_expense,@surrender_value,@net_compensate_expense,@net_contract_reserve,@policy_bonus_expense,@reinsure_expense,@other_business_cost,@operate_tax_add,@sale_expense,@manage_expense,@me_research_expense,@finance_expense,@fe_interest_expense,@fe_interest_income,@asset_impairment_loss,@credit_impairment_loss,@toc_other,@fairvalue_change_income,@invest_income,@invest_joint_income,@net_exposure_income,@exchange_income,@asset_disposal_income,@asset_impairment_income,@credit_impairment_income,@other_income,@operate_profit_other,@operate_profit_balance,@operate_profit,@nonbusiness_income,@noncurrent_disposal_income,@nonbusiness_expense,@noncurrent_disposal_loss,@effect_tp_other,@total_profit_balance,@total_profit,@income_tax,@effect_netprofit_other,@effect_netprofit_balance,@unconfirm_invest_loss,@netprofit,@precombine_profit,@continued_netprofit,@discontinued_netprofit,@parent_netprofit,@minority_interest,@deduct_parent_netprofit,@netprofit_other,@netprofit_balance,@basic_eps,@diluted_eps,@other_compre_income,@parent_oci,@minority_oci,@parent_oci_other,@parent_oci_balance,@unable_oci,@creditrisk_fairvalue_change,@otherright_fairvalue_change,@setup_profit_change,@rightlaw_unable_oci,@unable_oci_other,@unable_oci_balance,@able_oci,@rightlaw_able_oci,@afa_fairvalue_change,@hmi_afa,@cashflow_hedge_valid,@creditor_fairvalue_change,@creditor_impairment_reserve,@finance_oci_amt,@convert_diff,@able_oci_other,@able_oci_balance,@oci_other,@oci_balance,@total_compre_income,@parent_tci,@minority_tci,@precombine_tci,@effect_tci_balance,@tci_other,@tci_balance,@acf_end_income,@opinion_type)";
                    em_inc_common_v1 entity = new em_inc_common_v1()
                    {
                        secucode = jo.secucode,
                        security_code = jo.security_code,
                        report_date = jo.report_date.ConvertToDate(),
                        notice_date = jo.notice_date.ConvertToDate(),
                        update_date = jo.update_date.ConvertToDate(),
                        total_operate_income = jo.total_operate_income.ConvertToDecimal(),
                        operate_income = jo.operate_income.ConvertToDecimal(),
                        interest_income = jo.interest_income.ConvertToDecimal(),
                        earned_premium = jo.earned_premium.ConvertToDecimal(),
                        fee_commission_income = jo.fee_commission_income.ConvertToDecimal(),
                        other_business_income = jo.other_business_income.ConvertToDecimal(),
                        toi_other = jo.toi_other.ConvertToDecimal(),
                        total_operate_cost = jo.total_operate_cost.ConvertToDecimal(),
                        operate_cost = jo.operate_cost.ConvertToDecimal(),
                        interest_expense = jo.interest_expense.ConvertToDecimal(),
                        fee_commission_expense = jo.fee_commission_expense.ConvertToDecimal(),
                        research_expense = jo.research_expense.ConvertToDecimal(),
                        surrender_value = jo.surrender_value.ConvertToDecimal(),
                        net_compensate_expense = jo.net_compensate_expense.ConvertToDecimal(),
                        net_contract_reserve = jo.net_contract_reserve.ConvertToDecimal(),
                        policy_bonus_expense = jo.policy_bonus_expense.ConvertToDecimal(),
                        reinsure_expense = jo.reinsure_expense.ConvertToDecimal(),
                        other_business_cost = jo.other_business_cost.ConvertToDecimal(),
                        operate_tax_add = jo.operate_tax_add.ConvertToDecimal(),
                        sale_expense = jo.sale_expense.ConvertToDecimal(),
                        manage_expense = jo.manage_expense.ConvertToDecimal(),
                        me_research_expense = jo.me_research_expense.ConvertToDecimal(),
                        finance_expense = jo.finance_expense.ConvertToDecimal(),
                        fe_interest_expense = jo.fe_interest_expense.ConvertToDecimal(),
                        fe_interest_income = jo.fe_interest_income.ConvertToDecimal(),
                        asset_impairment_loss = jo.asset_impairment_loss.ConvertToDecimal(),
                        credit_impairment_loss = jo.credit_impairment_loss.ConvertToDecimal(),
                        toc_other = jo.toc_other.ConvertToDecimal(),
                        fairvalue_change_income = jo.fairvalue_change_income.ConvertToDecimal(),
                        invest_income = jo.invest_income.ConvertToDecimal(),
                        invest_joint_income = jo.invest_joint_income.ConvertToDecimal(),
                        net_exposure_income = jo.net_exposure_income.ConvertToDecimal(),
                        exchange_income = jo.exchange_income.ConvertToDecimal(),
                        asset_disposal_income = jo.asset_disposal_income.ConvertToDecimal(),
                        asset_impairment_income = jo.asset_impairment_income.ConvertToDecimal(),
                        credit_impairment_income = jo.credit_impairment_income.ConvertToDecimal(),
                        other_income = jo.other_income.ConvertToDecimal(),
                        operate_profit_other = jo.operate_profit_other.ConvertToDecimal(),
                        operate_profit_balance = jo.operate_profit_balance.ConvertToDecimal(),
                        operate_profit = jo.operate_profit.ConvertToDecimal(),
                        nonbusiness_income = jo.nonbusiness_income.ConvertToDecimal(),
                        noncurrent_disposal_income = jo.noncurrent_disposal_income.ConvertToDecimal(),
                        nonbusiness_expense = jo.nonbusiness_expense.ConvertToDecimal(),
                        noncurrent_disposal_loss = jo.noncurrent_disposal_loss.ConvertToDecimal(),
                        effect_tp_other = jo.effect_tp_other.ConvertToDecimal(),
                        total_profit_balance = jo.total_profit_balance.ConvertToDecimal(),
                        total_profit = jo.total_profit.ConvertToDecimal(),
                        income_tax = jo.income_tax.ConvertToDecimal(),
                        effect_netprofit_other = jo.effect_netprofit_other.ConvertToDecimal(),
                        effect_netprofit_balance = jo.effect_netprofit_balance.ConvertToDecimal(),
                        unconfirm_invest_loss = jo.unconfirm_invest_loss.ConvertToDecimal(),
                        netprofit = jo.netprofit.ConvertToDecimal(),
                        precombine_profit = jo.precombine_profit.ConvertToDecimal(),
                        continued_netprofit = jo.continued_netprofit.ConvertToDecimal(),
                        discontinued_netprofit = jo.discontinued_netprofit.ConvertToDecimal(),
                        parent_netprofit = jo.parent_netprofit.ConvertToDecimal(),
                        minority_interest = jo.minority_interest.ConvertToDecimal(),
                        deduct_parent_netprofit = jo.deduct_parent_netprofit.ConvertToDecimal(),
                        netprofit_other = jo.netprofit_other.ConvertToDecimal(),
                        netprofit_balance = jo.netprofit_balance.ConvertToDecimal(),
                        basic_eps = jo.basic_eps.ConvertToDecimal(),
                        diluted_eps = jo.diluted_eps.ConvertToDecimal(),
                        other_compre_income = jo.other_compre_income.ConvertToDecimal(),
                        parent_oci = jo.parent_oci.ConvertToDecimal(),
                        minority_oci = jo.minority_oci.ConvertToDecimal(),
                        parent_oci_other = jo.parent_oci_other.ConvertToDecimal(),
                        parent_oci_balance = jo.parent_oci_balance.ConvertToDecimal(),
                        unable_oci = jo.unable_oci.ConvertToDecimal(),
                        creditrisk_fairvalue_change = jo.creditrisk_fairvalue_change.ConvertToDecimal(),
                        otherright_fairvalue_change = jo.otherright_fairvalue_change.ConvertToDecimal(),
                        setup_profit_change = jo.setup_profit_change.ConvertToDecimal(),
                        rightlaw_unable_oci = jo.rightlaw_unable_oci.ConvertToDecimal(),
                        unable_oci_other = jo.unable_oci_other.ConvertToDecimal(),
                        unable_oci_balance = jo.unable_oci_balance.ConvertToDecimal(),
                        able_oci = jo.able_oci.ConvertToDecimal(),
                        rightlaw_able_oci = jo.rightlaw_able_oci.ConvertToDecimal(),
                        afa_fairvalue_change = jo.afa_fairvalue_change.ConvertToDecimal(),
                        hmi_afa = jo.hmi_afa.ConvertToDecimal(),
                        cashflow_hedge_valid = jo.cashflow_hedge_valid.ConvertToDecimal(),
                        creditor_fairvalue_change = jo.creditor_fairvalue_change.ConvertToDecimal(),
                        creditor_impairment_reserve = jo.creditor_impairment_reserve.ConvertToDecimal(),
                        finance_oci_amt = jo.finance_oci_amt.ConvertToDecimal(),
                        convert_diff = jo.convert_diff.ConvertToDecimal(),
                        able_oci_other = jo.able_oci_other.ConvertToDecimal(),
                        able_oci_balance = jo.able_oci_balance.ConvertToDecimal(),
                        oci_other = jo.oci_other.ConvertToDecimal(),
                        oci_balance = jo.oci_balance.ConvertToDecimal(),
                        total_compre_income = jo.total_compre_income.ConvertToDecimal(),
                        parent_tci = jo.parent_tci.ConvertToDecimal(),
                        minority_tci = jo.minority_tci.ConvertToDecimal(),
                        precombine_tci = jo.precombine_tci.ConvertToDecimal(),
                        effect_tci_balance = jo.effect_tci_balance.ConvertToDecimal(),
                        tci_other = jo.tci_other.ConvertToDecimal(),
                        tci_balance = jo.tci_balance.ConvertToDecimal(),
                        acf_end_income = jo.acf_end_income.ConvertToDecimal(),
                        opinion_type = StringUtils.FromUnicodeString(jo.opinion_type),

                    };
                    using (IDbConnection conn = new MySqlConnection(MySqlDbHelper.NICESHOTDB_CONN_STRING))
                    {
                        conn.Execute(sql, entity);
                    }
                }

                return edit_entity != null;
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("sync em report data occurs error: tscode={0};date={1},details:{2}", jo.secucode, jo.report_date, ex));
            }

            return false;
        }
        #endregion

        #region	em_cf_common_v1
        public static em_cf_common_v1 get_em_cf_common_v1_data(string ts_code, string date)
        {
            using (IDbConnection conn = new MySqlConnection(MySqlDbHelper.NICESHOTDB_CONN_STRING))
            {
                string sql = "select * from em_cf_common_v1 where secucode='" + ts_code + "' and report_date='" + date + "'";
                return conn.Query<em_cf_common_v1>(sql).FirstOrDefault();
            }
        }

        public static bool oper_em_cf_common_v1_data(em_cf_common_v1_jo jo, bool includeUpdate)
        {
            try
            {
                em_cf_common_v1 edit_entity = get_em_cf_common_v1_data(jo.secucode, jo.report_date);
                if (edit_entity != null)
                {
                    string sql = "update em_cf_common_v1 set  secucode=@secucode,security_code=@security_code,report_date=@report_date,notice_date=@notice_date,update_date=@update_date,sales_services=@sales_services,deposit_interbank_add=@deposit_interbank_add,loan_pbc_add=@loan_pbc_add,ofi_bf_add=@ofi_bf_add,receive_origic_premium=@receive_origic_premium,receive_reinsure_net=@receive_reinsure_net,insured_invest_add=@insured_invest_add,disposal_tfa_add=@disposal_tfa_add,receive_interest_commission=@receive_interest_commission,borrow_fund_add=@borrow_fund_add,loan_advance_reduce=@loan_advance_reduce,repo_business_add=@repo_business_add,receive_tax_refund=@receive_tax_refund,receive_other_operate=@receive_other_operate,operate_inflow_other=@operate_inflow_other,operate_inflow_balance=@operate_inflow_balance,total_operate_inflow=@total_operate_inflow,buy_services=@buy_services,loan_advance_add=@loan_advance_add,pbc_interbank_add=@pbc_interbank_add,pay_origic_compensate=@pay_origic_compensate,pay_interest_commission=@pay_interest_commission,pay_policy_bonus=@pay_policy_bonus,pay_staff_cash=@pay_staff_cash,pay_all_tax=@pay_all_tax,pay_other_operate=@pay_other_operate,operate_outflow_other=@operate_outflow_other,operate_outflow_balance=@operate_outflow_balance,total_operate_outflow=@total_operate_outflow,operate_netcash_other=@operate_netcash_other,operate_netcash_balance=@operate_netcash_balance,netcash_operate=@netcash_operate,withdraw_invest=@withdraw_invest,receive_invest_income=@receive_invest_income,disposal_long_asset=@disposal_long_asset,disposal_subsidiary_other=@disposal_subsidiary_other,reduce_pledge_timedeposits=@reduce_pledge_timedeposits,receive_other_invest=@receive_other_invest,invest_inflow_other=@invest_inflow_other,invest_inflow_balance=@invest_inflow_balance,total_invest_inflow=@total_invest_inflow,construct_long_asset=@construct_long_asset,invest_pay_cash=@invest_pay_cash,pledge_loan_add=@pledge_loan_add,obtain_subsidiary_other=@obtain_subsidiary_other,add_pledge_timedeposits=@add_pledge_timedeposits,pay_other_invest=@pay_other_invest,invest_outflow_other=@invest_outflow_other,invest_outflow_balance=@invest_outflow_balance,total_invest_outflow=@total_invest_outflow,invest_netcash_other=@invest_netcash_other,invest_netcash_balance=@invest_netcash_balance,netcash_invest=@netcash_invest,accept_invest_cash=@accept_invest_cash,subsidiary_accept_invest=@subsidiary_accept_invest,receive_loan_cash=@receive_loan_cash,issue_bond=@issue_bond,receive_other_finance=@receive_other_finance,finance_inflow_other=@finance_inflow_other,finance_inflow_balance=@finance_inflow_balance,total_finance_inflow=@total_finance_inflow,pay_debt_cash=@pay_debt_cash,assign_dividend_porfit=@assign_dividend_porfit,subsidiary_pay_dividend=@subsidiary_pay_dividend,buy_subsidiary_equity=@buy_subsidiary_equity,pay_other_finance=@pay_other_finance,subsidiary_reduce_cash=@subsidiary_reduce_cash,finance_outflow_other=@finance_outflow_other,finance_outflow_balance=@finance_outflow_balance,total_finance_outflow=@total_finance_outflow,finance_netcash_other=@finance_netcash_other,finance_netcash_balance=@finance_netcash_balance,netcash_finance=@netcash_finance,rate_change_effect=@rate_change_effect,cce_add_other=@cce_add_other,cce_add_balance=@cce_add_balance,cce_add=@cce_add,begin_cce=@begin_cce,end_cce_other=@end_cce_other,end_cce_balance=@end_cce_balance,end_cce=@end_cce,netprofit=@netprofit,asset_impairment=@asset_impairment,fa_ir_depr=@fa_ir_depr,oilgas_biology_depr=@oilgas_biology_depr,ir_depr=@ir_depr,ia_amortize=@ia_amortize,lpe_amortize=@lpe_amortize,defer_income_amortize=@defer_income_amortize,prepaid_expense_reduce=@prepaid_expense_reduce,accrued_expense_add=@accrued_expense_add,disposal_longasset_loss=@disposal_longasset_loss,fa_scrap_loss=@fa_scrap_loss,fairvalue_change_loss=@fairvalue_change_loss,finance_expense=@finance_expense,invest_loss=@invest_loss,defer_tax=@defer_tax,dt_asset_reduce=@dt_asset_reduce,dt_liab_add=@dt_liab_add,predict_liab_add=@predict_liab_add,inventory_reduce=@inventory_reduce,operate_rece_reduce=@operate_rece_reduce,operate_payable_add=@operate_payable_add,other=@other,operate_netcash_othernote=@operate_netcash_othernote,operate_netcash_balancenote=@operate_netcash_balancenote,netcash_operatenote=@netcash_operatenote,debt_transfer_capital=@debt_transfer_capital,convert_bond_1year=@convert_bond_1year,finlease_obtain_fa=@finlease_obtain_fa,uninvolve_investfin_other=@uninvolve_investfin_other,end_cash=@end_cash,begin_cash=@begin_cash,end_cash_equivalents=@end_cash_equivalents,begin_cash_equivalents=@begin_cash_equivalents,cce_add_othernote=@cce_add_othernote,cce_add_balancenote=@cce_add_balancenote,cce_addnote=@cce_addnote,opinion_type=@opinion_type,minority_interest=@minority_interest where id=@id";
                    if (includeUpdate)
                    {
                        using (IDbConnection conn = new MySqlConnection(MySqlDbHelper.NICESHOTDB_CONN_STRING))
                        {
                            conn.Execute(sql, new
                            {
                                secucode = jo.secucode,
                                security_code = jo.security_code,
                                report_date = jo.report_date.ConvertToDate(),
                                notice_date = jo.notice_date.ConvertToDate(),
                                update_date = jo.update_date.ConvertToDate(),
                                sales_services = jo.sales_services.ConvertToDecimal(),
                                deposit_interbank_add = jo.deposit_interbank_add.ConvertToDecimal(),
                                loan_pbc_add = jo.loan_pbc_add.ConvertToDecimal(),
                                ofi_bf_add = jo.ofi_bf_add.ConvertToDecimal(),
                                receive_origic_premium = jo.receive_origic_premium.ConvertToDecimal(),
                                receive_reinsure_net = jo.receive_reinsure_net.ConvertToDecimal(),
                                insured_invest_add = jo.insured_invest_add.ConvertToDecimal(),
                                disposal_tfa_add = jo.disposal_tfa_add.ConvertToDecimal(),
                                receive_interest_commission = jo.receive_interest_commission.ConvertToDecimal(),
                                borrow_fund_add = jo.borrow_fund_add.ConvertToDecimal(),
                                loan_advance_reduce = jo.loan_advance_reduce.ConvertToDecimal(),
                                repo_business_add = jo.repo_business_add.ConvertToDecimal(),
                                receive_tax_refund = jo.receive_tax_refund.ConvertToDecimal(),
                                receive_other_operate = jo.receive_other_operate.ConvertToDecimal(),
                                operate_inflow_other = jo.operate_inflow_other.ConvertToDecimal(),
                                operate_inflow_balance = jo.operate_inflow_balance.ConvertToDecimal(),
                                total_operate_inflow = jo.total_operate_inflow.ConvertToDecimal(),
                                buy_services = jo.buy_services.ConvertToDecimal(),
                                loan_advance_add = jo.loan_advance_add.ConvertToDecimal(),
                                pbc_interbank_add = jo.pbc_interbank_add.ConvertToDecimal(),
                                pay_origic_compensate = jo.pay_origic_compensate.ConvertToDecimal(),
                                pay_interest_commission = jo.pay_interest_commission.ConvertToDecimal(),
                                pay_policy_bonus = jo.pay_policy_bonus.ConvertToDecimal(),
                                pay_staff_cash = jo.pay_staff_cash.ConvertToDecimal(),
                                pay_all_tax = jo.pay_all_tax.ConvertToDecimal(),
                                pay_other_operate = jo.pay_other_operate.ConvertToDecimal(),
                                operate_outflow_other = jo.operate_outflow_other.ConvertToDecimal(),
                                operate_outflow_balance = jo.operate_outflow_balance.ConvertToDecimal(),
                                total_operate_outflow = jo.total_operate_outflow.ConvertToDecimal(),
                                operate_netcash_other = jo.operate_netcash_other.ConvertToDecimal(),
                                operate_netcash_balance = jo.operate_netcash_balance.ConvertToDecimal(),
                                netcash_operate = jo.netcash_operate.ConvertToDecimal(),
                                withdraw_invest = jo.withdraw_invest.ConvertToDecimal(),
                                receive_invest_income = jo.receive_invest_income.ConvertToDecimal(),
                                disposal_long_asset = jo.disposal_long_asset.ConvertToDecimal(),
                                disposal_subsidiary_other = jo.disposal_subsidiary_other.ConvertToDecimal(),
                                reduce_pledge_timedeposits = jo.reduce_pledge_timedeposits.ConvertToDecimal(),
                                receive_other_invest = jo.receive_other_invest.ConvertToDecimal(),
                                invest_inflow_other = jo.invest_inflow_other.ConvertToDecimal(),
                                invest_inflow_balance = jo.invest_inflow_balance.ConvertToDecimal(),
                                total_invest_inflow = jo.total_invest_inflow.ConvertToDecimal(),
                                construct_long_asset = jo.construct_long_asset.ConvertToDecimal(),
                                invest_pay_cash = jo.invest_pay_cash.ConvertToDecimal(),
                                pledge_loan_add = jo.pledge_loan_add.ConvertToDecimal(),
                                obtain_subsidiary_other = jo.obtain_subsidiary_other.ConvertToDecimal(),
                                add_pledge_timedeposits = jo.add_pledge_timedeposits.ConvertToDecimal(),
                                pay_other_invest = jo.pay_other_invest.ConvertToDecimal(),
                                invest_outflow_other = jo.invest_outflow_other.ConvertToDecimal(),
                                invest_outflow_balance = jo.invest_outflow_balance.ConvertToDecimal(),
                                total_invest_outflow = jo.total_invest_outflow.ConvertToDecimal(),
                                invest_netcash_other = jo.invest_netcash_other.ConvertToDecimal(),
                                invest_netcash_balance = jo.invest_netcash_balance.ConvertToDecimal(),
                                netcash_invest = jo.netcash_invest.ConvertToDecimal(),
                                accept_invest_cash = jo.accept_invest_cash.ConvertToDecimal(),
                                subsidiary_accept_invest = jo.subsidiary_accept_invest.ConvertToDecimal(),
                                receive_loan_cash = jo.receive_loan_cash.ConvertToDecimal(),
                                issue_bond = jo.issue_bond.ConvertToDecimal(),
                                receive_other_finance = jo.receive_other_finance.ConvertToDecimal(),
                                finance_inflow_other = jo.finance_inflow_other.ConvertToDecimal(),
                                finance_inflow_balance = jo.finance_inflow_balance.ConvertToDecimal(),
                                total_finance_inflow = jo.total_finance_inflow.ConvertToDecimal(),
                                pay_debt_cash = jo.pay_debt_cash.ConvertToDecimal(),
                                assign_dividend_porfit = jo.assign_dividend_porfit.ConvertToDecimal(),
                                subsidiary_pay_dividend = jo.subsidiary_pay_dividend.ConvertToDecimal(),
                                buy_subsidiary_equity = jo.buy_subsidiary_equity.ConvertToDecimal(),
                                pay_other_finance = jo.pay_other_finance.ConvertToDecimal(),
                                subsidiary_reduce_cash = jo.subsidiary_reduce_cash.ConvertToDecimal(),
                                finance_outflow_other = jo.finance_outflow_other.ConvertToDecimal(),
                                finance_outflow_balance = jo.finance_outflow_balance.ConvertToDecimal(),
                                total_finance_outflow = jo.total_finance_outflow.ConvertToDecimal(),
                                finance_netcash_other = jo.finance_netcash_other.ConvertToDecimal(),
                                finance_netcash_balance = jo.finance_netcash_balance.ConvertToDecimal(),
                                netcash_finance = jo.netcash_finance.ConvertToDecimal(),
                                rate_change_effect = jo.rate_change_effect.ConvertToDecimal(),
                                cce_add_other = jo.cce_add_other.ConvertToDecimal(),
                                cce_add_balance = jo.cce_add_balance.ConvertToDecimal(),
                                cce_add = jo.cce_add.ConvertToDecimal(),
                                begin_cce = jo.begin_cce.ConvertToDecimal(),
                                end_cce_other = jo.end_cce_other.ConvertToDecimal(),
                                end_cce_balance = jo.end_cce_balance.ConvertToDecimal(),
                                end_cce = jo.end_cce.ConvertToDecimal(),
                                netprofit = jo.netprofit.ConvertToDecimal(),
                                asset_impairment = jo.asset_impairment.ConvertToDecimal(),
                                fa_ir_depr = jo.fa_ir_depr.ConvertToDecimal(),
                                oilgas_biology_depr = jo.oilgas_biology_depr.ConvertToDecimal(),
                                ir_depr = jo.ir_depr.ConvertToDecimal(),
                                ia_amortize = jo.ia_amortize.ConvertToDecimal(),
                                lpe_amortize = jo.lpe_amortize.ConvertToDecimal(),
                                defer_income_amortize = jo.defer_income_amortize.ConvertToDecimal(),
                                prepaid_expense_reduce = jo.prepaid_expense_reduce.ConvertToDecimal(),
                                accrued_expense_add = jo.accrued_expense_add.ConvertToDecimal(),
                                disposal_longasset_loss = jo.disposal_longasset_loss.ConvertToDecimal(),
                                fa_scrap_loss = jo.fa_scrap_loss.ConvertToDecimal(),
                                fairvalue_change_loss = jo.fairvalue_change_loss.ConvertToDecimal(),
                                finance_expense = jo.finance_expense.ConvertToDecimal(),
                                invest_loss = jo.invest_loss.ConvertToDecimal(),
                                defer_tax = jo.defer_tax.ConvertToDecimal(),
                                dt_asset_reduce = jo.dt_asset_reduce.ConvertToDecimal(),
                                dt_liab_add = jo.dt_liab_add.ConvertToDecimal(),
                                predict_liab_add = jo.predict_liab_add.ConvertToDecimal(),
                                inventory_reduce = jo.inventory_reduce.ConvertToDecimal(),
                                operate_rece_reduce = jo.operate_rece_reduce.ConvertToDecimal(),
                                operate_payable_add = jo.operate_payable_add.ConvertToDecimal(),
                                other = jo.other.ConvertToDecimal(),
                                operate_netcash_othernote = jo.operate_netcash_othernote.ConvertToDecimal(),
                                operate_netcash_balancenote = jo.operate_netcash_balancenote.ConvertToDecimal(),
                                netcash_operatenote = jo.netcash_operatenote.ConvertToDecimal(),
                                debt_transfer_capital = jo.debt_transfer_capital.ConvertToDecimal(),
                                convert_bond_1year = jo.convert_bond_1year.ConvertToDecimal(),
                                finlease_obtain_fa = jo.finlease_obtain_fa.ConvertToDecimal(),
                                uninvolve_investfin_other = jo.uninvolve_investfin_other.ConvertToDecimal(),
                                end_cash = jo.end_cash.ConvertToDecimal(),
                                begin_cash = jo.begin_cash.ConvertToDecimal(),
                                end_cash_equivalents = jo.end_cash_equivalents.ConvertToDecimal(),
                                begin_cash_equivalents = jo.begin_cash_equivalents.ConvertToDecimal(),
                                cce_add_othernote = jo.cce_add_othernote.ConvertToDecimal(),
                                cce_add_balancenote = jo.cce_add_balancenote.ConvertToDecimal(),
                                cce_addnote = jo.cce_addnote.ConvertToDecimal(),
                                opinion_type = StringUtils.FromUnicodeString(jo.opinion_type),
                                minority_interest = jo.minority_interest.ConvertToDecimal(),

                                id = edit_entity.id
                            });
                        }
                        Logger.Info(string.Format("update data: tscode={0};date={1}", jo.secucode, jo.report_date));
                    }
                }
                else
                {
                    Logger.Info(string.Format("insert data: tscode={0};date={1}", jo.secucode, jo.report_date));
                    string sql = "insert into em_cf_common_v1 (secucode,security_code,report_date,notice_date,update_date,sales_services,deposit_interbank_add,loan_pbc_add,ofi_bf_add,receive_origic_premium,receive_reinsure_net,insured_invest_add,disposal_tfa_add,receive_interest_commission,borrow_fund_add,loan_advance_reduce,repo_business_add,receive_tax_refund,receive_other_operate,operate_inflow_other,operate_inflow_balance,total_operate_inflow,buy_services,loan_advance_add,pbc_interbank_add,pay_origic_compensate,pay_interest_commission,pay_policy_bonus,pay_staff_cash,pay_all_tax,pay_other_operate,operate_outflow_other,operate_outflow_balance,total_operate_outflow,operate_netcash_other,operate_netcash_balance,netcash_operate,withdraw_invest,receive_invest_income,disposal_long_asset,disposal_subsidiary_other,reduce_pledge_timedeposits,receive_other_invest,invest_inflow_other,invest_inflow_balance,total_invest_inflow,construct_long_asset,invest_pay_cash,pledge_loan_add,obtain_subsidiary_other,add_pledge_timedeposits,pay_other_invest,invest_outflow_other,invest_outflow_balance,total_invest_outflow,invest_netcash_other,invest_netcash_balance,netcash_invest,accept_invest_cash,subsidiary_accept_invest,receive_loan_cash,issue_bond,receive_other_finance,finance_inflow_other,finance_inflow_balance,total_finance_inflow,pay_debt_cash,assign_dividend_porfit,subsidiary_pay_dividend,buy_subsidiary_equity,pay_other_finance,subsidiary_reduce_cash,finance_outflow_other,finance_outflow_balance,total_finance_outflow,finance_netcash_other,finance_netcash_balance,netcash_finance,rate_change_effect,cce_add_other,cce_add_balance,cce_add,begin_cce,end_cce_other,end_cce_balance,end_cce,netprofit,asset_impairment,fa_ir_depr,oilgas_biology_depr,ir_depr,ia_amortize,lpe_amortize,defer_income_amortize,prepaid_expense_reduce,accrued_expense_add,disposal_longasset_loss,fa_scrap_loss,fairvalue_change_loss,finance_expense,invest_loss,defer_tax,dt_asset_reduce,dt_liab_add,predict_liab_add,inventory_reduce,operate_rece_reduce,operate_payable_add,other,operate_netcash_othernote,operate_netcash_balancenote,netcash_operatenote,debt_transfer_capital,convert_bond_1year,finlease_obtain_fa,uninvolve_investfin_other,end_cash,begin_cash,end_cash_equivalents,begin_cash_equivalents,cce_add_othernote,cce_add_balancenote,cce_addnote,opinion_type,minority_interest) values (@secucode,@security_code,@report_date,@notice_date,@update_date,@sales_services,@deposit_interbank_add,@loan_pbc_add,@ofi_bf_add,@receive_origic_premium,@receive_reinsure_net,@insured_invest_add,@disposal_tfa_add,@receive_interest_commission,@borrow_fund_add,@loan_advance_reduce,@repo_business_add,@receive_tax_refund,@receive_other_operate,@operate_inflow_other,@operate_inflow_balance,@total_operate_inflow,@buy_services,@loan_advance_add,@pbc_interbank_add,@pay_origic_compensate,@pay_interest_commission,@pay_policy_bonus,@pay_staff_cash,@pay_all_tax,@pay_other_operate,@operate_outflow_other,@operate_outflow_balance,@total_operate_outflow,@operate_netcash_other,@operate_netcash_balance,@netcash_operate,@withdraw_invest,@receive_invest_income,@disposal_long_asset,@disposal_subsidiary_other,@reduce_pledge_timedeposits,@receive_other_invest,@invest_inflow_other,@invest_inflow_balance,@total_invest_inflow,@construct_long_asset,@invest_pay_cash,@pledge_loan_add,@obtain_subsidiary_other,@add_pledge_timedeposits,@pay_other_invest,@invest_outflow_other,@invest_outflow_balance,@total_invest_outflow,@invest_netcash_other,@invest_netcash_balance,@netcash_invest,@accept_invest_cash,@subsidiary_accept_invest,@receive_loan_cash,@issue_bond,@receive_other_finance,@finance_inflow_other,@finance_inflow_balance,@total_finance_inflow,@pay_debt_cash,@assign_dividend_porfit,@subsidiary_pay_dividend,@buy_subsidiary_equity,@pay_other_finance,@subsidiary_reduce_cash,@finance_outflow_other,@finance_outflow_balance,@total_finance_outflow,@finance_netcash_other,@finance_netcash_balance,@netcash_finance,@rate_change_effect,@cce_add_other,@cce_add_balance,@cce_add,@begin_cce,@end_cce_other,@end_cce_balance,@end_cce,@netprofit,@asset_impairment,@fa_ir_depr,@oilgas_biology_depr,@ir_depr,@ia_amortize,@lpe_amortize,@defer_income_amortize,@prepaid_expense_reduce,@accrued_expense_add,@disposal_longasset_loss,@fa_scrap_loss,@fairvalue_change_loss,@finance_expense,@invest_loss,@defer_tax,@dt_asset_reduce,@dt_liab_add,@predict_liab_add,@inventory_reduce,@operate_rece_reduce,@operate_payable_add,@other,@operate_netcash_othernote,@operate_netcash_balancenote,@netcash_operatenote,@debt_transfer_capital,@convert_bond_1year,@finlease_obtain_fa,@uninvolve_investfin_other,@end_cash,@begin_cash,@end_cash_equivalents,@begin_cash_equivalents,@cce_add_othernote,@cce_add_balancenote,@cce_addnote,@opinion_type,@minority_interest)";
                    em_cf_common_v1 entity = new em_cf_common_v1()
                    {
                        secucode = jo.secucode,
                        security_code = jo.security_code,
                        report_date = jo.report_date.ConvertToDate(),
                        notice_date = jo.notice_date.ConvertToDate(),
                        update_date = jo.update_date.ConvertToDate(),
                        sales_services = jo.sales_services.ConvertToDecimal(),
                        deposit_interbank_add = jo.deposit_interbank_add.ConvertToDecimal(),
                        loan_pbc_add = jo.loan_pbc_add.ConvertToDecimal(),
                        ofi_bf_add = jo.ofi_bf_add.ConvertToDecimal(),
                        receive_origic_premium = jo.receive_origic_premium.ConvertToDecimal(),
                        receive_reinsure_net = jo.receive_reinsure_net.ConvertToDecimal(),
                        insured_invest_add = jo.insured_invest_add.ConvertToDecimal(),
                        disposal_tfa_add = jo.disposal_tfa_add.ConvertToDecimal(),
                        receive_interest_commission = jo.receive_interest_commission.ConvertToDecimal(),
                        borrow_fund_add = jo.borrow_fund_add.ConvertToDecimal(),
                        loan_advance_reduce = jo.loan_advance_reduce.ConvertToDecimal(),
                        repo_business_add = jo.repo_business_add.ConvertToDecimal(),
                        receive_tax_refund = jo.receive_tax_refund.ConvertToDecimal(),
                        receive_other_operate = jo.receive_other_operate.ConvertToDecimal(),
                        operate_inflow_other = jo.operate_inflow_other.ConvertToDecimal(),
                        operate_inflow_balance = jo.operate_inflow_balance.ConvertToDecimal(),
                        total_operate_inflow = jo.total_operate_inflow.ConvertToDecimal(),
                        buy_services = jo.buy_services.ConvertToDecimal(),
                        loan_advance_add = jo.loan_advance_add.ConvertToDecimal(),
                        pbc_interbank_add = jo.pbc_interbank_add.ConvertToDecimal(),
                        pay_origic_compensate = jo.pay_origic_compensate.ConvertToDecimal(),
                        pay_interest_commission = jo.pay_interest_commission.ConvertToDecimal(),
                        pay_policy_bonus = jo.pay_policy_bonus.ConvertToDecimal(),
                        pay_staff_cash = jo.pay_staff_cash.ConvertToDecimal(),
                        pay_all_tax = jo.pay_all_tax.ConvertToDecimal(),
                        pay_other_operate = jo.pay_other_operate.ConvertToDecimal(),
                        operate_outflow_other = jo.operate_outflow_other.ConvertToDecimal(),
                        operate_outflow_balance = jo.operate_outflow_balance.ConvertToDecimal(),
                        total_operate_outflow = jo.total_operate_outflow.ConvertToDecimal(),
                        operate_netcash_other = jo.operate_netcash_other.ConvertToDecimal(),
                        operate_netcash_balance = jo.operate_netcash_balance.ConvertToDecimal(),
                        netcash_operate = jo.netcash_operate.ConvertToDecimal(),
                        withdraw_invest = jo.withdraw_invest.ConvertToDecimal(),
                        receive_invest_income = jo.receive_invest_income.ConvertToDecimal(),
                        disposal_long_asset = jo.disposal_long_asset.ConvertToDecimal(),
                        disposal_subsidiary_other = jo.disposal_subsidiary_other.ConvertToDecimal(),
                        reduce_pledge_timedeposits = jo.reduce_pledge_timedeposits.ConvertToDecimal(),
                        receive_other_invest = jo.receive_other_invest.ConvertToDecimal(),
                        invest_inflow_other = jo.invest_inflow_other.ConvertToDecimal(),
                        invest_inflow_balance = jo.invest_inflow_balance.ConvertToDecimal(),
                        total_invest_inflow = jo.total_invest_inflow.ConvertToDecimal(),
                        construct_long_asset = jo.construct_long_asset.ConvertToDecimal(),
                        invest_pay_cash = jo.invest_pay_cash.ConvertToDecimal(),
                        pledge_loan_add = jo.pledge_loan_add.ConvertToDecimal(),
                        obtain_subsidiary_other = jo.obtain_subsidiary_other.ConvertToDecimal(),
                        add_pledge_timedeposits = jo.add_pledge_timedeposits.ConvertToDecimal(),
                        pay_other_invest = jo.pay_other_invest.ConvertToDecimal(),
                        invest_outflow_other = jo.invest_outflow_other.ConvertToDecimal(),
                        invest_outflow_balance = jo.invest_outflow_balance.ConvertToDecimal(),
                        total_invest_outflow = jo.total_invest_outflow.ConvertToDecimal(),
                        invest_netcash_other = jo.invest_netcash_other.ConvertToDecimal(),
                        invest_netcash_balance = jo.invest_netcash_balance.ConvertToDecimal(),
                        netcash_invest = jo.netcash_invest.ConvertToDecimal(),
                        accept_invest_cash = jo.accept_invest_cash.ConvertToDecimal(),
                        subsidiary_accept_invest = jo.subsidiary_accept_invest.ConvertToDecimal(),
                        receive_loan_cash = jo.receive_loan_cash.ConvertToDecimal(),
                        issue_bond = jo.issue_bond.ConvertToDecimal(),
                        receive_other_finance = jo.receive_other_finance.ConvertToDecimal(),
                        finance_inflow_other = jo.finance_inflow_other.ConvertToDecimal(),
                        finance_inflow_balance = jo.finance_inflow_balance.ConvertToDecimal(),
                        total_finance_inflow = jo.total_finance_inflow.ConvertToDecimal(),
                        pay_debt_cash = jo.pay_debt_cash.ConvertToDecimal(),
                        assign_dividend_porfit = jo.assign_dividend_porfit.ConvertToDecimal(),
                        subsidiary_pay_dividend = jo.subsidiary_pay_dividend.ConvertToDecimal(),
                        buy_subsidiary_equity = jo.buy_subsidiary_equity.ConvertToDecimal(),
                        pay_other_finance = jo.pay_other_finance.ConvertToDecimal(),
                        subsidiary_reduce_cash = jo.subsidiary_reduce_cash.ConvertToDecimal(),
                        finance_outflow_other = jo.finance_outflow_other.ConvertToDecimal(),
                        finance_outflow_balance = jo.finance_outflow_balance.ConvertToDecimal(),
                        total_finance_outflow = jo.total_finance_outflow.ConvertToDecimal(),
                        finance_netcash_other = jo.finance_netcash_other.ConvertToDecimal(),
                        finance_netcash_balance = jo.finance_netcash_balance.ConvertToDecimal(),
                        netcash_finance = jo.netcash_finance.ConvertToDecimal(),
                        rate_change_effect = jo.rate_change_effect.ConvertToDecimal(),
                        cce_add_other = jo.cce_add_other.ConvertToDecimal(),
                        cce_add_balance = jo.cce_add_balance.ConvertToDecimal(),
                        cce_add = jo.cce_add.ConvertToDecimal(),
                        begin_cce = jo.begin_cce.ConvertToDecimal(),
                        end_cce_other = jo.end_cce_other.ConvertToDecimal(),
                        end_cce_balance = jo.end_cce_balance.ConvertToDecimal(),
                        end_cce = jo.end_cce.ConvertToDecimal(),
                        netprofit = jo.netprofit.ConvertToDecimal(),
                        asset_impairment = jo.asset_impairment.ConvertToDecimal(),
                        fa_ir_depr = jo.fa_ir_depr.ConvertToDecimal(),
                        oilgas_biology_depr = jo.oilgas_biology_depr.ConvertToDecimal(),
                        ir_depr = jo.ir_depr.ConvertToDecimal(),
                        ia_amortize = jo.ia_amortize.ConvertToDecimal(),
                        lpe_amortize = jo.lpe_amortize.ConvertToDecimal(),
                        defer_income_amortize = jo.defer_income_amortize.ConvertToDecimal(),
                        prepaid_expense_reduce = jo.prepaid_expense_reduce.ConvertToDecimal(),
                        accrued_expense_add = jo.accrued_expense_add.ConvertToDecimal(),
                        disposal_longasset_loss = jo.disposal_longasset_loss.ConvertToDecimal(),
                        fa_scrap_loss = jo.fa_scrap_loss.ConvertToDecimal(),
                        fairvalue_change_loss = jo.fairvalue_change_loss.ConvertToDecimal(),
                        finance_expense = jo.finance_expense.ConvertToDecimal(),
                        invest_loss = jo.invest_loss.ConvertToDecimal(),
                        defer_tax = jo.defer_tax.ConvertToDecimal(),
                        dt_asset_reduce = jo.dt_asset_reduce.ConvertToDecimal(),
                        dt_liab_add = jo.dt_liab_add.ConvertToDecimal(),
                        predict_liab_add = jo.predict_liab_add.ConvertToDecimal(),
                        inventory_reduce = jo.inventory_reduce.ConvertToDecimal(),
                        operate_rece_reduce = jo.operate_rece_reduce.ConvertToDecimal(),
                        operate_payable_add = jo.operate_payable_add.ConvertToDecimal(),
                        other = jo.other.ConvertToDecimal(),
                        operate_netcash_othernote = jo.operate_netcash_othernote.ConvertToDecimal(),
                        operate_netcash_balancenote = jo.operate_netcash_balancenote.ConvertToDecimal(),
                        netcash_operatenote = jo.netcash_operatenote.ConvertToDecimal(),
                        debt_transfer_capital = jo.debt_transfer_capital.ConvertToDecimal(),
                        convert_bond_1year = jo.convert_bond_1year.ConvertToDecimal(),
                        finlease_obtain_fa = jo.finlease_obtain_fa.ConvertToDecimal(),
                        uninvolve_investfin_other = jo.uninvolve_investfin_other.ConvertToDecimal(),
                        end_cash = jo.end_cash.ConvertToDecimal(),
                        begin_cash = jo.begin_cash.ConvertToDecimal(),
                        end_cash_equivalents = jo.end_cash_equivalents.ConvertToDecimal(),
                        begin_cash_equivalents = jo.begin_cash_equivalents.ConvertToDecimal(),
                        cce_add_othernote = jo.cce_add_othernote.ConvertToDecimal(),
                        cce_add_balancenote = jo.cce_add_balancenote.ConvertToDecimal(),
                        cce_addnote = jo.cce_addnote.ConvertToDecimal(),
                        opinion_type = StringUtils.FromUnicodeString(jo.opinion_type),
                        minority_interest = jo.minority_interest.ConvertToDecimal(),

                    };
                    using (IDbConnection conn = new MySqlConnection(MySqlDbHelper.NICESHOTDB_CONN_STRING))
                    {
                        conn.Execute(sql, entity);
                    }
                }

                return edit_entity != null;
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("sync em report data occurs error: tscode={0};date={1},details:{2}", jo.secucode, jo.report_date, ex));
            }

            return false;
        }
        #endregion


        #endregion


    }
}
