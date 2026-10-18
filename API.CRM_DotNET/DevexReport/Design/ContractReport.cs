using API.CRM_DotNET.Documents;

namespace API.CRM_DotNET.DevexReport.Design
{
    /// <summary>
    /// Hợp đồng mua bán. Edit the layout in Visual Studio: right-click this file → View Designer.
    /// Fields come from <see cref="DocumentData"/> (Field List → objectDataSource1).
    /// The terms section (richTerms in sub-band sbTerms) is filled at runtime from a Word/RTF file in
    /// DevexReport/Template; its [placeholders] are merged with the same data.
    /// </summary>
    public partial class ContractReport : DevExpress.XtraReports.UI.XtraReport
    {
        public ContractReport()
        {
            InitializeComponent();
        }

        /// <summary>Supplies the data to print (the designer only knows the DocumentData type).</summary>
        public void Bind(DocumentData data)
        {
            objectDataSource1.DataSource = data;
            // Contracts made from an opportunity have no item list: skip the empty items table.
            DetailReport.Visible = data.HasLines;
        }

        /// <summary>Loads the terms from a .docx/.rtf template, or hides the section when there is none.</summary>
        public void SetTerms(string? templatePath)
        {
            if (templatePath is null)
            {
                sbTerms.Visible = false;
                return;
            }
            // LoadFile(path) detects the format (.docx / .rtf) from the file itself.
            richTerms.LoadFile(templatePath);
        }
    }
}
