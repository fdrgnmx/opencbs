﻿// Octopus MFS is an integrated suite for managing a Micro Finance Institution: 
// clients, contracts, accounting, reporting and risk
// Copyright © 2006,2007 OCTO Technology & OXUS Development Network
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License along
// with this program; if not, write to the Free Software Foundation, Inc.,
// 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
//
// Website: http://www.opencbs.com
// Contact: contact@opencbs.com

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using OpenCBS.ArchitectureV2.CommandData;
using OpenCBS.ArchitectureV2.Interface;
using OpenCBS.ArchitectureV2.Interface.View;
using OpenCBS.ArchitectureV2.Message;
using OpenCBS.CoreDomain;
using OpenCBS.CoreDomain.Clients;
using OpenCBS.CoreDomain.Contracts.Loans;
using OpenCBS.Enums;
using OpenCBS.Extensions;
using OpenCBS.GUI.Accounting;
using OpenCBS.GUI.AuditTrail;
using OpenCBS.GUI.Clients;
using OpenCBS.GUI.Configuration;
using OpenCBS.GUI.Contracts;
using OpenCBS.GUI.Database;
using OpenCBS.GUI.Products;
using OpenCBS.GUI.Report_Browser;
using OpenCBS.GUI.TellerManagement;
using OpenCBS.GUI.Tools;
using OpenCBS.GUI.UserControl;
using OpenCBS.MultiLanguageRessources;
using OpenCBS.Reports;
using OpenCBS.Reports.Forms;
using OpenCBS.Services;
using OpenCBS.Shared;
using OpenCBS.Shared.Settings;

namespace OpenCBS.GUI
{
    public partial class MainView : SweetBaseForm, IMainView
    {
        [ImportMany(typeof(IMenu), RequiredCreationPolicy = CreationPolicy.Shared)]
        public List<IMenu> ExtensionMenuItems { get; set; }

        [ImportMany(typeof(IInitializer))]
        public List<IInitializer> ExtensionInitalizers { get; set; }

        private List<MenuObject> _menuItems;
        private bool _showTellerFormOnClose = true;
        private readonly IApplicationController _applicationController;

        public MainView(IApplicationController applicationController)
        {
            InitializeComponent();
            try
            {
                _applicationController = applicationController;
                _applicationController.Subscribe<ShowViewMessage>(this, OnShowView);
                _applicationController.Subscribe<ActivateViewMessage>(this, OnActivateView);
                _applicationController.Subscribe<StartPageShownMessage>(this, OnStartPageShown);
                _applicationController.Subscribe<StartPageHiddenMessage>(this, OnStartPageHidden);
                _applicationController.Subscribe<AlertsShownMessage>(this, OnAlertsShown);
                _applicationController.Subscribe<AlertsHiddenMessage>(this, OnAlertsHidden);
                _applicationController.Subscribe<DashboardShownMessage>(this, OnDashboardShown);
                _applicationController.Subscribe<DashboardHiddenMessage>(this, OnDashboardHidden);
                _applicationController.Subscribe<SearchShownMessage>(this, OnSearchShown);
                _applicationController.Subscribe<SearchHiddenMessage>(this, OnSearchHidden);
                _applicationController.Subscribe<EditLoanMessage>(this, OnEditLoan);
                _applicationController.Subscribe<EditSavingMessage>(this, OnEditSaving);
                _applicationController.Subscribe<RestartApplicationMessage>(this, m =>
                {
                    RestartApplication(m.Language);
                });
                SetUp();
                MefContainer.Current.Bind(this);
                _menuItems = new List<MenuObject>();
                _menuItems = Services.GetMenuItemServices().GetMenuList(OSecurityObjectTypes.MenuItem);
                LoadReports();
                LoadReportsToolStrip();
                InitializeTracer();
                DisplayWinFormDetails();
                InitMenu();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetUp()
        {
            _startPageItem.Click += (sender, e) => _applicationController.Execute(new ShowStartPageCommandData());
            _alertsItem.Click += (sender, e) => _applicationController.Execute(new ShowAlertsCommandData());
            _dashboardItem.Click += (sender, e) => _applicationController.Execute(new ShowDashboardCommandData());
            _searchItem.Click += (sender, e) => _applicationController.Execute(new ShowSearchCommandData());
        }

        private void OnShowView(ShowViewMessage message)
        {
            var form = (Form) message.View;
            form.MdiParent = this;
            form.WindowState = FormWindowState.Maximized;
            form.Show();
        }

        private static void OnActivateView(ActivateViewMessage message)
        {
            var form = (Form) message.View;
            form.BringToFront();
        }

        private void OnStartPageShown(StartPageShownMessage message)
        {
            _startPageItem.Checked = true;
        }

        private void OnStartPageHidden(StartPageHiddenMessage message)
        {
            _startPageItem.Checked = false;
        }

        private void OnAlertsShown(AlertsShownMessage message)
        {
            _alertsItem.Checked = true;
        }

        private void OnAlertsHidden(AlertsHiddenMessage message)
        {
            _alertsItem.Checked = false;
        }

        private void OnDashboardShown(DashboardShownMessage message)
        {
            _dashboardItem.Checked = true;
        }

        private void OnDashboardHidden(DashboardHiddenMessage message)
        {
            _dashboardItem.Checked = false;
        }

        private void OnSearchShown(SearchShownMessage message)
        {
            _searchItem.Checked = true;
        }

        private void OnSearchHidden(SearchHiddenMessage message)
        {
            _searchItem.Checked = false;
        }

        private void OnEditLoan(EditLoanMessage message)
        {
            var client = ServicesProvider.GetInstance().GetClientServices().FindTiersByContractId(message.Id);
            InitializeCreditContractForm(client, message.Id);
        }

        private void OnEditSaving(EditSavingMessage message)
        {
            var client = ServicesProvider.GetInstance().GetClientServices().FindTiersBySavingsId(message.Id);
            InitializeSavingContractForm(client, message.Id);
        }

        private void InitMenu()
        {
            tellersToolStripMenuItem.Visible = ServicesProvider.GetInstance().GetGeneralSettings().UseTellerManagement;
        }

        private void InitializeTracer()
        {
            string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Trace.AutoFlush = true;
            Trace.WriteLine("Application has started");
        }

        private void DisplayWinFormDetails()
        {
            _DisplayDetails();
            InitializeContractCurrencies();
        }

        private void InitializeContractCurrencies()
        {
            mnuChartOfAccounts.Click += mnuChartOfAccounts_Click;
        }

        private void InitExtensions()
        {
            foreach (var initializer in ExtensionInitalizers)
            {
                initializer.Init();
            }
        }

        private bool InitializeTellerManagement()
        {
            if (ServicesProvider.GetInstance().GetGeneralSettings().UseTellerManagement)
            {
                FrmOpenCloseTeller frm = new FrmOpenCloseTeller(true);
                frm.ShowDialog();

                if (frm.DialogResult == DialogResult.OK)
                {
                    if (frm.Teller != null && frm.Teller.Id != 0)
                    {
                        Teller.CurrentTeller = frm.Teller;
                        //tellerManagementToolStripMenuItem.Visible = true;
                        ServicesProvider.GetInstance().GetEventProcessorServices().LogUser(OUserEvents.UserOpenTellerEvent,
                            Teller.CurrentTeller.Name + " opened", User.CurrentUser.Id);
                        ServicesProvider.GetInstance().GetEventProcessorServices().FireTellerEvent(frm.OpenOfDayAmountEvent);

                        if (frm.OpenAmountPositiveDifferenceEvent != null)
                            ServicesProvider.GetInstance().GetEventProcessorServices().FireTellerEvent(
                                frm.OpenAmountPositiveDifferenceEvent);
                        else if (frm.OpenAmountNegativeDifferenceEvent != null)
                            ServicesProvider.GetInstance().GetEventProcessorServices().FireTellerEvent(
                                frm.OpenAmountNegativeDifferenceEvent);

                    }

                    return true;
                }
                return false;

            }
            return true;
        }

        private void _DisplayDetails()
        {
            mainStatusBarLblUserName.Text = String.Format("{0} ({1})", User.CurrentUser.FirstName, User.CurrentUser.UserRole);
            toolStripStatusLblDB.Text = !TechnicalSettings.UseOnlineMode ?
                String.Format(" {0}", TechnicalSettings.DatabaseName) :
                "Online";

            toolBarLblVersion.Text = String.Format("OpenCBS {0}", TechnicalSettings.SoftwareVersion);
            if (TechnicalSettings.UseOnlineMode)
                menuItemDatabaseControlPanel.Visible = false;
        }

        private void _InitializeUserRights()
        {
            foreach (ToolStripMenuItem mi in MainMenuStrip.Items)
            {
                Role role = User.CurrentUser.UserRole;
                MenuObject foundMo = GetMenuObject(mi.Name);

                if (foundMo != null)
                {
                    mi.Enabled = role.IsMenuAllowed(foundMo);
                    mi.Tag = foundMo;
                    InitializeMenuChildren(mi, role);
                }
            }
        }

        private void InitializeMenuChildren(ToolStripMenuItem pMenuItem, Role pRole)
        {
            if (!pMenuItem.HasDropDownItems)
            {
                return;
            }
            foreach (Object tsmi in pMenuItem.DropDownItems)
            {
                if (!(tsmi is ToolStripMenuItem))
                    continue;

                ToolStripMenuItem tsmiMenu = (ToolStripMenuItem)tsmi;

                MenuObject foundMO = GetMenuObject(tsmiMenu.Name);
                bool isAllowed = foundMO == null || pRole.IsMenuAllowed(foundMO);
                tsmiMenu.Enabled = isAllowed;
                tsmiMenu.Tag = foundMO;

                InitializeMenuChildren(tsmiMenu, pRole);
            }
        }

        private MenuObject GetMenuObject(string pText)
        {
            MenuObject foundObject = _menuItems.Find(item => item == pText.Trim());
            return foundObject;
        }

        private void SetActiveMenuItem(ToolStripMenuItem tsmi_menu)
        {
            if (!tsmi_menu.HasDropDownItems)
            {
                return;
            }
            foreach (Object mnu in tsmi_menu.DropDownItems)
            {
                if (mnu is ToolStripMenuItem)
                {
                    SetActiveMenuItem((ToolStripMenuItem)mnu);
                }
            }
        }

        private void DisplayFastChoiceForm()
        {
            foreach (var item in MainMenuStrip.Items.OfType<ToolStripMenuItem>())
            {
                SetActiveMenuItem(item);
            }
            Role role = User.CurrentUser.UserRole;
            role.DefaultStartPage = ServicesProvider.GetInstance().GetRoleServices().GetRolesDefaultStartPageByRoleId(role.Id);
            switch (role.DefaultStartPage)
            {
                case OStartPages.StartPages.START_PAGE:
                    _applicationController.Execute(new ShowStartPageCommandData());
                    break;
                case OStartPages.StartPages.DASHBOARD_PAGE:
                    _applicationController.Execute(new ShowDashboardCommandData());
                    break;
                case OStartPages.StartPages.ALERTS_PAGE:
                    _applicationController.Execute(new ShowAlertsCommandData());
                    break;
            }
        }

        public void InitializePersonForm()
        {
            ClientForm personForm = new ClientForm(OClientTypes.Person, this, false, _applicationController) { MdiParent = this };
            personForm.Show();
        }

        public void InitializeCorporateForm()
        {
            ClientForm corporateForm = new ClientForm(OClientTypes.Corporate, this, false, _applicationController) { MdiParent = this };
            corporateForm.Show();
        }
        public void InitializeCorporateForm(Corporate corporate, Project project)
        {
            ClientForm corporateForm = new ClientForm(corporate, this, _applicationController) { MdiParent = this };
            if (project != null)
                corporateForm.DisplayUserControl_ViewProject(project, null);

            corporateForm.Show();
        }

        public void InitializePersonForm(Person person, Project project)
        {
            ClientForm personForm = new ClientForm(person, this, _applicationController)
            {
                MdiParent = this,
                Text = string.Format(
                       "{0} [{1}]",
                       MultiLanguageStrings.GetString(Ressource.ClientForm, "Person.Text"),
                       person.Name)
            };
            if (project != null)
                personForm.DisplayUserControl_ViewProject(project, null);
            personForm.Show();
        }

        public void InitializeGroupForm()
        {
            ClientForm personForm = new ClientForm(OClientTypes.Group, this, false, _applicationController) { MdiParent = this };
            personForm.Show();
        }

        public void InitializeVillageForm()
        {
            NonSolidaryGroupForm frm = new NonSolidaryGroupForm(_applicationController) { MdiParent = this };
            frm.Show();
        }

        public void InitializeVillageForm(Village village)
        {
            NonSolidaryGroupForm frm = new NonSolidaryGroupForm(village, _applicationController) { MdiParent = this };
            frm.Show();
        }

        public void InitializeGroupForm(Group group, Project project)
        {
            ClientForm personForm = new ClientForm(group, this, _applicationController)
            {
                MdiParent = this,
                Text =
                    string.Format("{0} [{1}]", MultiLanguageStrings.GetString(Ressource.ClientForm, "Group.Text"),
                                  group.Name)
            };
            if (project != null)
                personForm.DisplayUserControl_ViewProject(project, null);
            personForm.Show();
        }

        public void InitializeSearchClientForm()
        {
            SearchClientForm searchClientForm = SearchClientForm.GetInstance(this);
            searchClientForm.BringToFront();
            searchClientForm.WindowState = FormWindowState.Normal;
            searchClientForm.Show();
        }

        public void InitializeSearchCreditContractForm()
        {
            SearchCreditContractForm searchCreditContractForm = SearchCreditContractForm.GetInstance(this);
            searchCreditContractForm.BringToFront();
            searchCreditContractForm.WindowState = FormWindowState.Normal;
            searchCreditContractForm.Show();
        }

        public void InitializeCreditContractForm(IClient pClient, int pContractId)
        {
            /*
             * This code is for loading compulsory savings. Compulsory savings are being 
             * loaded here because in LoanManager class SavingsManager trigers problems.
             * Ruslan Kazakov
             */

            if (pClient.Projects != null)
                foreach (Project project in pClient.Projects)
                    if (project.Credits != null)
                        foreach (Loan loan in project.Credits)
                            loan.CompulsorySavings = ServicesProvider.GetInstance().GetSavingServices().GetSavingForLoan(loan.Id, true);
            ClientForm personForm = new ClientForm(pClient, pContractId, this, _applicationController) { MdiParent = this };
            personForm.Show();
        }

        public void InitializeSavingContractForm(IClient client, int savingId)
        {
            switch (client.Type)
            {
                case OClientTypes.Person:
                    {
                        var personForm = new ClientForm((Person)client, this, _applicationController)
                        {
                            MdiParent = this,
                            Text = string.Format("{0} [{1}]", MultiLanguageStrings.GetString(
                            Ressource.ClientForm, "Person.Text"),
                            ((Person)client).Name)
                        };
                        personForm.DisplaySaving(savingId, client);
                        personForm.Show();
                        break;
                    }
                case OClientTypes.Group:
                    {
                        var personForm = new ClientForm((Group)client, this, _applicationController)
                        {
                            MdiParent = this,
                            Text = string.Format("{0} [{1}]", MultiLanguageStrings.GetString(Ressource.ClientForm, "Group.Text"), ((Group)client).Name)
                        };
                        personForm.DisplaySaving(savingId, client);
                        personForm.Show();
                        break;
                    }
                case OClientTypes.Village:
                    {
                        var frm = new NonSolidaryGroupForm((Village)client, _applicationController) { MdiParent = this };
                        frm.Show();
                        break;
                    }
                case OClientTypes.Corporate:
                    {
                        var corporateForm = new ClientForm((Corporate)client, this, _applicationController) { MdiParent = this };
                        corporateForm.DisplaySaving(savingId, client);
                        corporateForm.Show();
                        break;
                    }
            }
        }

        public void InitializeChartOfAccountsForm(int pCurrencyId)
        {
            var chartOfAccountsForm = new ChartOfAccountsForm(pCurrencyId) { MdiParent = this };
            chartOfAccountsForm.Show();
        }

        private void InitializeCollateralProductsForm()
        {
            var collateralProductsForm = new FrmAvalaibleCollateralProducts { MdiParent = this };
            collateralProductsForm.Show();
        }

        private void InitializePackagesForm()
        {
            var packagesForm = new FrmAvalaibleLoanProducts { MdiParent = this };
            packagesForm.Show();
        }

        private void InitializeSavingProductsForm()
        {
            var frmSavingProductsForm = new FrmAvailableSavingProducts { MdiParent = this };
            frmSavingProductsForm.Show();
        }

        private void InitializeReassingContractsForm()
        {
            var reassingForm = new ReassignContractsForm { MdiParent = this };
            reassingForm.Show();
        }

        private static void InitializeDomainOfApplicationForm()
        {
            var doaf = new FrmEconomicActivity();
            //doaf.Show();
            doaf.ShowDialog();
        }

        private static void InitializeDomainOfApplicationForm(bool isLoanPurpose)
        {
            var doaf = new FrmEconomicActivity(isLoanPurpose);
            //doaf.Show();
            doaf.ShowDialog();
        }

        public void SetInfoMessage(string pMessage)
        {
        }

        private void mnuNewPerson_Click(object sender, EventArgs e)
        {
            InitializePersonForm();
        }

        private void mnuNewGroup_Click(object sender, EventArgs e)
        {
            InitializeGroupForm();
        }

        private void mnuSearchClient_Click(object sender, EventArgs e)
        {
            InitializeSearchClientForm();
        }

        private void mnuChartOfAccounts_Click(object sender, EventArgs e)
        {
            InitializeChartOfAccountsForm(ServicesProvider.GetInstance().GetCurrencyServices().GetPivot().Id);
        }

        private void menuItemPackages_Click(object sender, EventArgs e)
        {
            InitializePackagesForm();
        }

        private void savingProductsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InitializeSavingProductsForm();
        }

        private void mnuDomainOfApplication_Click(object sender, EventArgs e)
        {
            InitializeDomainOfApplicationForm();
        }

        private void menuItemExportTransaction_Click(object sender, EventArgs e)
        {
            Form exportTransactions = new ExportBookingsForm { MdiParent = this };

            exportTransactions.Show();
        }
        private void menuItemExchangeRate_Click(object sender, System.EventArgs e)
        {
            ExchangeRateForm exchangeRate = new ExchangeRateForm();
            exchangeRate.Show();
        }

        private void mnuSearchContract_Click(object sender, System.EventArgs e)
        {
            InitializeSearchCreditContractForm();
        }

        private void menuItemAddUser_Click(object sender, System.EventArgs e)
        {
            UserForm userForm = new UserForm { MdiParent = this };
            userForm.Show();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            mainStatusBarLblDate.ForeColor = TimeProvider.IsUsingSystemTime ? Color.Black : Color.Red;
            mainStatusBarLblDate.Text = String.Format("{0} {1}", TimeProvider.Today.ToString("dd/MM/yyyy"),
                                                      TimeProvider.Now.ToLongTimeString());
        }

        private void menuItemSetting_Click(object sender, EventArgs e)
        {
            FrmGeneralSettings generalSettings = new FrmGeneralSettings();
            generalSettings.ShowDialog();
        }

        private void OnAboutMenuItemClick(object sender, EventArgs e)
        {
            AboutForm aboutForm = new AboutForm();
            aboutForm.ShowDialog();
        }

        private void OnChangeApplicationDateClick(object sender, EventArgs e)
        {
            ApplicationDate frm = new ApplicationDate
            {
                Today = TimeProvider.Today
            };

            if (frm.ShowDialog() != DialogResult.OK) return;
            if (TimeProvider.Today == frm.Today) return;

            TimeProvider.SetToday(frm.Today);
        }


        private void menuItemBackupData_Click(object sender, EventArgs e)
        {
            FrmDatabaseSettings frmDatabaseSettings = new FrmDatabaseSettings(FrmDatabaseSettingsEnum.SqlServerSettings, false, true);
            frmDatabaseSettings.ShowDialog();
        }

        private void FillDropDownMenuWithLanguages()
        {
            string currentLanguage = UserSettings.Language;

            frenchToolStripMenuItem.Checked = (currentLanguage == "fr");
            russianToolStripMenuItem.Checked = (currentLanguage == "ru-RU");
            englishToolStripMenuItem.Checked = (currentLanguage == "en-US");
            spanishToolStripMenuItem.Checked = (currentLanguage == "es-ES");
        }

        private void _InitializeStandardBookings()
        {
            StandardBooking standardBooking = new StandardBooking { MdiParent = this };
            standardBooking.Show();
        }

        private void toolStripMenuItemAccountView_Click(object sender, EventArgs e)
        {
            AccountView accountView = new AccountView { MdiParent = this };
            accountView.Show();
        }

        private void menuItemLocations_Click(object sender, EventArgs e)
        {
            Form frm = new FrmLocations();
            frm.ShowDialog();
        }

        private void toolStripMenuItemFundingLines_Click(object sender, EventArgs e)
        {
            Form frm = new FrmFundingLine { MdiParent = this };
            frm.Show();
        }

        private void RestartApplication(string language)
        {
            UserSettings.SetUserLanguage(language);
            UserSettings.Language = language;
            ServicesProvider.GetInstance().GetEventProcessorServices().LogUser(
                OUserEvents.UserCloseTellerEvent,
                OUserEvents.UserCloseTellerDescription,
                User.CurrentUser.Id);

            MessageBox.Show(MultiLanguageStrings.GetString(Ressource.MainView, "advancedSettingsChanged.Text"));
            Restart.LaunchRestarter();
        }

        private void LanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string language = sender == frenchToolStripMenuItem ? "fr" :
                    (sender == russianToolStripMenuItem ? "ru-RU" :
                    (sender == englishToolStripMenuItem ? "en-US" :
                    (sender == spanishToolStripMenuItem ? "es-ES" : "pt")));

            if (ServicesProvider.GetInstance().GetGeneralSettings().UseTellerManagement)
            {
                if (Teller.CurrentTeller != null && Teller.CurrentTeller.Id != 0)
                {
                    FrmOpenCloseTeller frm = new FrmOpenCloseTeller(false);
                    frm.ShowDialog();

                    if (frm.DialogResult == DialogResult.OK)
                    {
                        _showTellerFormOnClose = false;
                        Teller.CurrentTeller = null;
                        ServicesProvider.GetInstance().GetEventProcessorServices().FireTellerEvent(
                                                                                frm.CloseOfDayAmountEvent);
                        if (frm.CloseAmountNegativeDifferenceEvent != null)
                            ServicesProvider.GetInstance().GetEventProcessorServices().FireTellerEvent(
                                frm.CloseAmountNegativeDifferenceEvent);
                        else if (frm.CloseAmountPositiveDifferenceEvent != null)
                            ServicesProvider.GetInstance().GetEventProcessorServices().FireTellerEvent(
                                frm.CloseAmountPositiveDifferenceEvent);
                        RestartApplication(language);
                    }
                }
            }
            else RestartApplication(language);
        }

        private void toolStripMenuItemInstallmentTypes_Click(object sender, EventArgs e)
        {
            FrmInstallmentTypes frmInstallmentTypes = new FrmInstallmentTypes();
            frmInstallmentTypes.ShowDialog();
        }

        private void reasignToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InitializeReassingContractsForm();
        }

        private void newCorporateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InitializeCorporateForm();
        }

        public void InitializePersonForm(Person member)
        {
            throw new NotImplementedException();
        }

        private void mnuNewVillage_Click(object sender, EventArgs e)
        {
            InitializeVillageForm();
        }

        private void miContractCode_Click(object sender, EventArgs e)
        {
            ContractCodeForm frm = new ContractCodeForm();
            if (DialogResult.OK == frm.ShowDialog())
            {
                ServicesProvider.GetInstance().GetGeneralSettings().UpdateParameter(OGeneralSettings.CONTRACT_CODE_TEMPLATE, frm.code);
                ServicesProvider.GetInstance().GetApplicationSettingsServices().UpdateSelectedParameter(OGeneralSettings.CONTRACT_CODE_TEMPLATE, frm.code);
            }
        }

        private void LotrasmicMainWindowForm_Load(object sender, EventArgs e)
        {
            InitExtensions();
            UserSettings.Language = UserSettings.GetUserLanguage();
            if (InitializeTellerManagement())
            {
                Ping();
                LogUser();
                InitializeMainMenu();
                _InitializeUserRights();
                DisplayFastChoiceForm();
            }
            else
            {
                Environment.Exit(0);
            }
        }

        private static void Ping()
        {
            var worker = new BackgroundWorker();
            worker.DoWork += (sender, args) =>
            {
                var mfiService = ServicesProvider.GetInstance().GetMFIServices();
                var pingInfo = mfiService.GetPingInfo();
                var appSettingsService = ServicesProvider.GetInstance().GetApplicationSettingsServices();
                var guid = appSettingsService.GetGuid();
                if (guid == null)
                {
                    guid = Guid.NewGuid();
                    appSettingsService.SetGuid(guid.Value);
                }
                var collection = new Dictionary<string, string>
                {
                    { "Guid", guid.ToString() },
                    { "Username", User.CurrentUser.UserName },
                    { "Version", TechnicalSettings.GetDisplayVersion() },
                    { "Olb", pingInfo.Olb.ToString("0") },
                    { "NumberOfIndividualClients", pingInfo.NumberOfIndividualClients.ToString("0") },
                    { "NumberOfSolidarityGroups", pingInfo.NumberOfSolidarityGroups.ToString("0") },
                    { "NumberOfNonSolidarityGroups", pingInfo.NumberOfNonSolidarityGroups.ToString("0") },
                    { "NumberOfCompanies", pingInfo.NumberOfCompanies.ToString("0") }
                };
                var parameters = string.Join("&", collection.Select(x => string.Format("{0}={1}", x.Key, x.Value)).ToArray());
                var data = Encoding.UTF8.GetBytes(parameters);
                var request = (HttpWebRequest)WebRequest.Create("http://opencbsping.apphb.com/Ping");
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = data.Length;
                request.UserAgent = "OpenCBS";
                request.Timeout = 5000;
                try
                {
                    using (var stream = request.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }
                }
                catch
                {

                }
            };
            worker.RunWorkerCompleted += (sender, args) =>
            {
                if (args.Error != null)
                    Debug.WriteLine(args.Error.Message);
            };
            worker.RunWorkerAsync();
        }

        private void LoadReportsToolStrip()
        {
            ToolStripMenuItem i;
            IComparer<Report> comparer = new ReportAbcComparer();
            var re = new List<Report>(ReportService.GetInstance().GetReports());
            re.Sort(comparer);
            foreach (Report report in re)
            {
                i = new ToolStripMenuItem(report.Title) { Tag = report.Name };
                i.Click += new System.EventHandler(this.activeLoansToolStripMenuItem_Click);
                reportsToolStripMenuItem.DropDownItems.Add(i);
            }


        }

        private void LogUser()
        {
            ServicesProvider.GetInstance().GetEventProcessorServices().LogUser(OUserEvents.UserLogInEvent,
                OUserEvents.UserLoginDescription, User.CurrentUser.Id);
        }

        private static void LoadReports()
        {
            bwReportLoader_DoWork(null, null);
        }

        private void InitializeMainMenu()
        {
            InitializeCoreMenu();
            foreach (var extensionItem in ExtensionMenuItems)
            {
                var anchor = mainMenu.Items.Find(extensionItem.InsertAfter, true).FirstOrDefault();
                if (anchor == null) continue;

                var owner = (ToolStripMenuItem)anchor.OwnerItem;

                var temp = extensionItem;

                var items = owner == null ? mainMenu.Items : owner.DropDownItems;
                var index = items.IndexOf(anchor);

                if (extensionItem.GetItem().Name == "mnuAccountancy" && !ServicesProvider.GetInstance().GetGeneralSettings().UseExternalAccounting)
                    continue;


                items.Insert(index + 1, temp.GetItem());
            }
            var names = ExtensionMenuItems.Select(i => i.GetItem().Name).ToList();
            foreach (var menu in _applicationController.GetAllInstances<IMenu>())
            {
                if (names.Contains(menu.GetItem().Name)) continue;
                var anchor = mainMenu.Items.Find(menu.InsertAfter, true).FirstOrDefault();
                if (anchor == null) continue;

                var owner = (ToolStripMenuItem) anchor.OwnerItem;

                var temp = menu;

                var items = owner == null ? mainMenu.Items : owner.DropDownItems;
                var index = items.IndexOf(anchor);

                items.Insert(index + 1, temp.GetItem());
            }
        }

        private void InitializeCoreMenu()
        {
            mainMenu.Items["mnuAccounting"].Visible = !ServicesProvider.GetInstance().GetGeneralSettings().UseExternalAccounting;
        }

        private static void bwReportLoader_DoWork(object sender, DoWorkEventArgs e)
        {
            ReportService rs = ReportService.GetInstance();
            rs.LoadReports();
        }

        private void standardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _InitializeStandardBookings();
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start("chrome.exe", url);
            }
            catch
            {
                try
                {
                    Process.Start("firefox.exe", url);
                }
                catch
                {
                    try
                    {
                        Process.Start("iexplore.exe", url);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void nIUpdateAvailable_BalloonTipClicked(object sender, EventArgs e)
        {
            OpenUrl(nIUpdateAvailable.Tag.ToString());
            nIUpdateAvailable.Visible = false;
        }

        private void currenciesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmCurrencyType _frmCurrency = new FrmCurrencyType();
            _frmCurrency.Show();
        }

        private void eventsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AuditTrailForm trailForm = new AuditTrailForm { MdiParent = this };
            trailForm.Show();
        }

        private void LotrasmicMainWindowForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _applicationController.Unsubscribe(this);
            if (ServicesProvider.GetInstance().GetGeneralSettings().UseTellerManagement)
            {
                if (_showTellerFormOnClose)
                {
                    e.Cancel = false;

                    if (Teller.CurrentTeller != null && Teller.CurrentTeller.Id != 0)
                        if (!CloseTeller())
                            e.Cancel = true;
                }
            }
            try
            {
                ServicesProvider.GetInstance().GetEventProcessorServices().LogUser(OUserEvents.UserLogOutEvent,
                    OUserEvents.UserLogoutDescription, User.CurrentUser.Id);
            }
            catch { }
        }

        private void accountingRulesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmAccountingRules frmAccountingRules = new FrmAccountingRules { MdiParent = this };
            frmAccountingRules.Show();
        }

        private void rolesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmRoles rolesForm = new FrmRoles(this) { MdiParent = this };
            rolesForm.Show();
        }

        private void trialBalanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AccountTrialBalance accountTrialBalance = new AccountTrialBalance { MdiParent = this };
            accountTrialBalance.Show();
        }

        private void changePasswordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PasswordForm pswdForm = new PasswordForm(User.CurrentUser);
            if (DialogResult.OK != pswdForm.ShowDialog()) return;
            User.CurrentUser.Password = pswdForm.NewPassword;
            ServicesProvider.GetInstance().GetUserServices().SaveUser(User.CurrentUser);
            Notify("passwordChanged");
        }

        private void manualEntriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ManualEntries accountView = new ManualEntries { MdiParent = this };
            accountView.Show();
        }

        private void branchesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BranchesForm frm = new BranchesForm { MdiParent = this };
            frm.Show();
        }

        private void closeTellerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!CloseTeller())
                MessageBox.Show(MultiLanguageStrings.GetString(Ressource.FrmOpenCloseTeller, "noOpenTellersText"));
        }

        private bool CloseTeller()
        {
            if (Teller.CurrentTeller != null)
            {
                FrmOpenCloseTeller frm = new FrmOpenCloseTeller(false);
                frm.ShowDialog();
                if (frm.DialogResult == DialogResult.OK)
                {
                    string desc = Teller.CurrentTeller.Name + " closed";
                    Teller.CurrentTeller = null;
                    ServicesProvider.GetInstance().GetEventProcessorServices().LogUser(
                                                                        OUserEvents.UserCloseTellerEvent,
                                                                        desc,
                                                                        User.CurrentUser.Id);
                    ServicesProvider.GetInstance().GetEventProcessorServices().FireTellerEvent(frm.CloseOfDayAmountEvent);
                    if (frm.CloseAmountNegativeDifferenceEvent != null)
                        ServicesProvider.GetInstance().GetEventProcessorServices().FireTellerEvent(
                            frm.CloseAmountNegativeDifferenceEvent);
                    else if (frm.CloseAmountPositiveDifferenceEvent != null)
                        ServicesProvider.GetInstance().GetEventProcessorServices().FireTellerEvent(
                            frm.CloseAmountPositiveDifferenceEvent);
                    return true;
                }
            }

            return false;
        }

        private void newClosureToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            AccountingClosureForm frm = new AccountingClosureForm { MdiParent = this };
            frm.Show();
        }

        private void fiscalYearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FiscalYear fiscalYear = new FiscalYear() { MdiParent = this };
            fiscalYear.Show();
        }

        private void tellersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TellersForm frm = new TellersForm() { MdiParent = this };
            frm.Show();
        }

        private void activeLoansToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                var reportName = (sender as ToolStripMenuItem).Tag.ToString();
                var report = ReportService.GetInstance().GetReportByName(reportName);
                var reportParamsForm = new ReportParamsForm(report.Params, report.Title);

                if (reportParamsForm.ShowDialog() != DialogResult.OK) return;

                var progressForm = new ReportLoadingProgressForm();
                progressForm.Show();

                var bw = new BackgroundWorker
                {
                    WorkerReportsProgress = true,
                    WorkerSupportsCancellation = true,
                };
                bw.DoWork += (obj, args) =>
                {
                    ReportService.GetInstance().LoadReport(report);
                    bw.ReportProgress(100);
                };
                bw.RunWorkerCompleted += (obj, args) =>
                {
                    progressForm.Close();
                    if (args.Error != null)
                    {
                        Fail(args.Error.Message);
                        return;
                    }
                    if (args.Cancelled) return;

                    report.OpenCount++;
                    report.SaveOpenCount();
                    var reportViewer = new ReportViewerForm(report);
                    reportViewer.Show();
                };
                bw.RunWorkerAsync(report);
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
            }
        }

        public void Run()
        {
            Show();
        }

        private void _aboutModulesMenuItem_Click(object sender, EventArgs e)
        {
            OpenUrl("http://opencbs.com/en/Additional-Modules/");
        }

        private void contactMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                const string url = "mailto:contact@opencbs.com?subject=I have a question about OpenCBS";
                Process.Start(url);
            }
            catch
            {
            }
        }

        private void OpenUserGuid(object sender, EventArgs e)
        {
            try
            {
                const string url = "http://opencbs.com/uploads/User%20guide%20OpenCBS%2014.11.pdf";
                Process.Start(url);
            }
            catch
            {}
        }

        private void getHelpFromForumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                const string url = "http://opencbs.freeforums.net/";
                Process.Start(url);
            }
            catch
            { }
        }

        private void visitOpenCBScomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                const string url = "http://opencbs.com";
                Process.Start(url);
            }
            catch
            { }
        }

        private void collateralProductsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InitializeCollateralProductsForm();
        }

        private void loanPurposeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InitializeDomainOfApplicationForm(true);
        }
    }
}
