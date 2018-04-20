namespace B1SimplificationInterface
{
    partial class StoreSyncConfirmationForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listView1 = new System.Windows.Forms.ListView();
            this.bn_continue = new System.Windows.Forms.Button();
            this.bn_back = new System.Windows.Forms.Button();
            this.col_type = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.col_sbs_no = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.col_store_no = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.col_no = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.col_date = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.col_sid = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // listView1
            // 
            this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.col_type,
            this.col_sbs_no,
            this.col_store_no,
            this.col_no,
            this.col_date,
            this.col_sid});
            this.listView1.Location = new System.Drawing.Point(11, 34);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(651, 450);
            this.listView1.TabIndex = 0;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            // 
            // bn_continue
            // 
            this.bn_continue.Location = new System.Drawing.Point(561, 493);
            this.bn_continue.Name = "bn_continue";
            this.bn_continue.Size = new System.Drawing.Size(103, 31);
            this.bn_continue.TabIndex = 1;
            this.bn_continue.Text = "Continue";
            this.bn_continue.UseVisualStyleBackColor = true;
            this.bn_continue.Click += new System.EventHandler(this.bn_continue_Click);
            // 
            // bn_back
            // 
            this.bn_back.Location = new System.Drawing.Point(468, 493);
            this.bn_back.Name = "bn_back";
            this.bn_back.Size = new System.Drawing.Size(87, 31);
            this.bn_back.TabIndex = 2;
            this.bn_back.Text = "Back";
            this.bn_back.UseVisualStyleBackColor = true;
            this.bn_back.Click += new System.EventHandler(this.bn_back_Click);
            // 
            // col_type
            // 
            this.col_type.Text = "Type";
            this.col_type.Width = 50;
            // 
            // col_sbs_no
            // 
            this.col_sbs_no.Text = "Sbs_no";
            // 
            // col_store_no
            // 
            this.col_store_no.Text = "Store_no";
            // 
            // col_no
            // 
            this.col_no.Text = "Doc_no";
            this.col_no.Width = 90;
            // 
            // col_date
            // 
            this.col_date.Text = "Date";
            this.col_date.Width = 100;
            // 
            // col_sid
            // 
            this.col_sid.Text = "SID";
            this.col_sid.Width = 200;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 14);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(448, 17);
            this.label1.TabIndex = 3;
            this.label1.Text = "Unverified slips and ASNs have been found. Do you wish to continue?";
            // 
            // StoreSyncConfirmationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(674, 530);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.bn_back);
            this.Controls.Add(this.bn_continue);
            this.Controls.Add(this.listView1);
            this.Name = "StoreSyncConfirmationForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "StoreSyncConfirmationForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.Button bn_continue;
        private System.Windows.Forms.Button bn_back;
        private System.Windows.Forms.ColumnHeader col_type;
        private System.Windows.Forms.ColumnHeader col_sbs_no;
        private System.Windows.Forms.ColumnHeader col_store_no;
        private System.Windows.Forms.ColumnHeader col_no;
        private System.Windows.Forms.ColumnHeader col_date;
        private System.Windows.Forms.ColumnHeader col_sid;
        private System.Windows.Forms.Label label1;
    }
}