using API.CRM_DotNET.Documents;

namespace API.CRM_DotNET.DevexReport.Design
{
    /// <summary>
    /// Đơn đặt hàng. Edit the layout in Visual Studio: right-click this file → View Designer.
    /// Fields come from <see cref="DocumentData"/> (Field List → objectDataSource1).
    /// </summary>
    public partial class PurchaseOrderReport : DevExpress.XtraReports.UI.XtraReport
    {
        public PurchaseOrderReport()
        {
            InitializeComponent();
        }

        /// <summary>Supplies the data to print (the designer only knows the DocumentData type).</summary>
        public void Bind(DocumentData data) => objectDataSource1.DataSource = data;
    }
}
