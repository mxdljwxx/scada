﻿/*
 * Copyright 2016 Mikhail Shiryaev
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Product  : Rapid SCADA
 * Module   : PlgTable
 * Summary  : Command dialog web form
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2016
 * Modified : 2016
 */

using Scada.Client;
using Scada.Data.Models;
using Scada.Data.Tables;
using Scada.UI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;

namespace Scada.Web.Plugins.Table
{
    /// <summary>
    /// Command dialog web form
    /// <para>Веб-форма диалога отправки команды ТУ</para>
    /// </summary>
    public partial class WFrmCommand : System.Web.UI.Page
    {
        /// <summary>
        /// Дискретная команда
        /// </summary>
        private struct DiscreteCmd
        {
            public int Val { get; set; }
            public string Text { get; set; }
        }


        private AppData appData;   // общие данные веб-приложения
        private UserData userData; // данные пользователя приложения
        private bool cmdEnabled;   // отправка команды разрешена
        private int ctrlCnlNum;    // номер канала управления


        /// <summary>
        /// Получить список дискретных команд
        /// </summary>
        private List<DiscreteCmd> GetDiscreteCmds(string[] cmdValArr)
        {
            List<DiscreteCmd> discreteCmds = new List<DiscreteCmd>();
            for (int i = 0, len = cmdValArr.Length; i < len; i++)
            {
                discreteCmds.Add(new DiscreteCmd() { Val = i, Text = cmdValArr[i] });
            }
            return discreteCmds;
        }

        /// <summary>
        /// Попытаться получить данные бинарной команды, введённые пользователем
        /// </summary>
        private bool TryParseCmdData(out byte[] cmdData)
        {
            if (txtCmdData.Text == "")
            {
                cmdData = null;
                return false;
            }
            else if (rbStr.Checked)
            {
                cmdData = Encoding.Default.GetBytes(txtCmdData.Text);
                return true;
            }
            else
            {
                return ScadaUtils.HexToBytes(txtCmdData.Text, out cmdData, true);
            }
        }

        /// <summary>
        /// Скрыть сообщение об ошибке
        /// </summary>
        private void HideErrMsg()
        {
            pnlErrMsg.Visible = false;
            lblCtrlCnlNotFound.Visible = false;
            lblWrongPwd.Visible = false;
            lblNoRights.Visible = false;
            lblIncorrectCmdVal.Visible = false;
            lblIncorrectCmdData.Visible = false;
        }

        /// <summary>
        /// Вывести сообщение об ошибке
        /// </summary>
        private void ShowErrMsg(Label lblMessage)
        {
            pnlErrMsg.Visible = true;
            lblMessage.Visible = true;
        }

        /// <summary>
        /// Вывести информацию о неудачном результате отправки команды
        /// </summary>
        private void ShowFailResult(Label lblMessage)
        {
            pnlFail.Visible = true;
            lblMessage.Visible = true;
        }

        /// <summary>
        /// Проверить пароль, если он используется
        /// </summary>
        private bool CheckPassword()
        {
            if (pnlPassword.Visible)
            {
                int roleID = BaseValues.Roles.Err; // неверный пароль
                bool reqOK = txtPassword.Text == "" ? 
                    true : appData.ServerComm.CheckUser(userData.UserProps.UserName, txtPassword.Text, out roleID);

                if (reqOK)
                {
                    if (roleID == BaseValues.Roles.Disabled || roleID == BaseValues.Roles.App) 
                    {
                        ShowErrMsg(lblNoRights);
                        return false;
                    }
                    else if (roleID == BaseValues.Roles.Err)
                    {
                        ShowErrMsg(lblWrongPwd);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    ShowCmdResult(false, false);
                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Отправить стандартную команду
        /// </summary>
        private void SendStandardCmd(double cmdVal)
        {
            bool result;
            bool sendOK = appData.ServerComm.SendStandardCommand(
                userData.UserProps.UserID, ctrlCnlNum, cmdVal, out result);
            ShowCmdResult(sendOK, result);
        }

        /// <summary>
        /// Отправить бинарную команду
        /// </summary>
        private void SendBinaryCmd(byte[] cmdData)
        {
            bool result;
            bool sendOK = appData.ServerComm.SendBinaryCommand(
                userData.UserProps.UserID, ctrlCnlNum, cmdData, out result);
            ShowCmdResult(sendOK, result);
        }

        /// <summary>
        /// Отправить команду опроса КП
        /// </summary>
        private void SendRequestCmd(int kpNum)
        {
            bool result;
            bool sendOK = appData.ServerComm.SendRequestCommand(
                userData.UserProps.UserID, ctrlCnlNum, kpNum, out result);
            ShowCmdResult(sendOK, result);
        }

        /// <summary>
        /// Отобразить результат выполнения команды
        /// </summary>
        private void ShowCmdResult(bool sendOK, bool result)
        {
            mvCommand.SetActiveView(viewCmdResult);
            btnSubmit.Enabled = false;

            if (sendOK && result)
            {
                pnlSuccess.Visible = true;
                ClientScript.RegisterStartupScript(GetType(), "Startup", "startCountdown();", true);
            }
            else if (sendOK)
            {
                ShowFailResult(lblCmdRejected);
            }
            else
            {
                ShowFailResult(lblCmdNotSent);
            }
        }


        protected void Page_Load(object sender, EventArgs e)
        {
            appData = AppData.GetAppData();
            userData = UserData.GetUserData();

            // проверка входа в систему
            if (!userData.LoggedOn)
                throw new ScadaException(CommonPhrases.NoRights);

            // скрытие сообщения об ошибке
            HideErrMsg();

            if (IsPostBack)
            {
                cmdEnabled = (bool)ViewState["CmdEnabled"];
                ctrlCnlNum = (int)ViewState["CtrlCnlNum"];
            }
            else
            {
                // получение параметров запроса и сохранение во ViewState
                int viewID;
                int.TryParse(Request.QueryString["viewID"], out viewID);
                int.TryParse(Request.QueryString["ctrlCnlNum"], out ctrlCnlNum);

                ViewState["CtrlCnlNum"] = ctrlCnlNum;

                // проверка прав
                if (!userData.UserRights.GetViewRights(viewID).ControlRight ||
                    !userData.WebSettings.CmdEnabled)
                    throw new ScadaException(CommonPhrases.NoRights);

                Type viewType = userData.UserViews.GetViewType(viewID);
                BaseView view = appData.ViewCache.GetView(viewType, viewID, true);

                if (!view.ContainsCtrlCnl(ctrlCnlNum))
                    throw new ScadaException(CommonPhrases.NoRights);

                // перевод веб-страницы
                Translator.TranslatePage(Page, "Scada.Web.Plugins.Table.WFrmCommand");

                // получение канала управления
                CtrlCnlProps ctrlCnlProps = appData.DataAccess.GetCtrlCnlProps(ctrlCnlNum);
                ViewState["CmdEnabled"] = ctrlCnlProps != null;

                if (ctrlCnlProps == null)
                {
                    // вывести сообщение, что канал управления не найден
                    lblCtrlCnlNotFound.Text = string.Format(lblCtrlCnlNotFound.Text, ctrlCnlNum);
                    ShowErrMsg(lblCtrlCnlNotFound);
                    btnSubmit.Enabled = false;
                }
                else
                {
                    // вывод информации по каналу управления
                    pnlInfo.Visible = true;
                    lblCtrlCnl.Text = HttpUtility.HtmlEncode(
                        string.Format("[{0}] {1}", ctrlCnlProps.CtrlCnlNum, ctrlCnlProps.CtrlCnlName));
                    lblObj.Text = HttpUtility.HtmlEncode(ctrlCnlProps.ObjNum > 0 ? 
                        string.Format("[{0}] {1}", ctrlCnlProps.ObjNum, ctrlCnlProps.ObjName) : "");
                    lblDev.Text = HttpUtility.HtmlEncode(ctrlCnlProps.KPNum > 0 ?
                        string.Format("[{0}] {1}", ctrlCnlProps.KPNum, ctrlCnlProps.KPName) : "");

                    // установка видимости поля для ввода пароля
                    pnlPassword.Visible = userData.WebSettings.CmdPassword;

                    // настройка элементов управления в зависимости от типа команды
                    switch (ctrlCnlProps.CmdTypeID)
                    {
                        case BaseValues.CmdTypes.Standard:
                            if (ctrlCnlProps.CmdValArr == null)
                            {
                                pnlRealValue.Visible = true;
                            }
                            else
                            {
                                repCommands.DataSource = GetDiscreteCmds(ctrlCnlProps.CmdValArr);
                                repCommands.DataBind();
                                pnlDiscreteValue.Visible = true;
                                btnSubmit.Visible = false;
                            }
                            break;
                        case BaseValues.CmdTypes.Binary:
                            pnlData.Visible = true;
                            break;
                        default: // BaseValues.CmdTypes.Request:
                            ViewState["KPNum"] = ctrlCnlProps.KPNum;
                            break;
                    }
                }
            }
        }

        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            if (cmdEnabled && CheckPassword())
            {
                if (pnlRealValue.Visible)
                {
                    // отправка стандартной команды для вещественных значений
                    double cmdVal;
                    if (ScadaUtils.TryParseDouble(txtCmdVal.Text, out cmdVal))
                        SendStandardCmd(cmdVal);
                    else
                        ShowErrMsg(lblIncorrectCmdVal);
                }
                else if (pnlData.Visible)
                {
                    // отправка бинарной команды
                    byte[] cmdData;
                    if (TryParseCmdData(out cmdData))
                        SendBinaryCmd(cmdData);
                    else
                        ShowErrMsg(lblIncorrectCmdData);
                }
                else
                {
                    // отправка команды опроса КП
                    int kpNum = (int)ViewState["KPNum"];
                    SendRequestCmd(kpNum);
                }
            }
        }

        protected void repCommands_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (cmdEnabled && CheckPassword())
            {
                // отправка стандартной команды для дискретных значений
                Button btn = (Button)e.CommandSource;
                int cmdVal = int.Parse(btn.Attributes["data-cmdval"]);
                SendStandardCmd(cmdVal);
            }
        }
    }
}