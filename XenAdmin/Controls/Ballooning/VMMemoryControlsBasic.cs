﻿/* Copyright (c) Citrix Systems Inc. 
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met: 
 * 
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer. 
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution. 
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using XenAPI;
using XenAdmin.Commands;


namespace XenAdmin.Controls.Ballooning
{
    public partial class VMMemoryControlsBasic : VMMemoryControlsEdit
    {
        public event EventHandler InstallTools;

        public VMMemoryControlsBasic()
        {
            InitializeComponent();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (vms == null || vms.Count == 0)
                return;

            // If !firstPaint, don't re-intialize, because it will pull the rug from under our own edits.
            if (!firstPaint)
                return;

            // Layout because of different fonts. I tried putting this in the constructor but it didn't take effect that early.
            memorySpinnerFixed.Left = radioOff.Right + radioOff.Margin.Right;

            // Calculate the maximum legal value of dynamic minimum
            CalcMaxDynMin();

            // Shiny bar
            vmShinyBar.Initialize(vm0, vms.Count > 1, CalcMemoryUsed(), true);

            // Radio buttons and "DMC Unavailable" warning
            if (ballooning)
            {
                if (vm0.memory_dynamic_min == vm0.memory_static_max)
                    radioOff.Checked = true;
                else
                    radioOn.Checked = true;
                iconDMCUnavailable.Visible = labelDMCUnavailable.Visible = linkInstallTools.Visible = false;
            }
            else
            {
                radioOff.Checked = true;
                radioOn.Enabled = false;
                groupBoxOn.Enabled = false;

                if (vms.Count > 1)
                {
                    // If all the Virtualisation Statuses are the same, report that reason.
                    // Otherwise give a generic message.
                    VM.VirtualisationStatus vs0 = vm0.GetVirtualisationStatus;
                    bool identical = true;
                    foreach (VM vm in vms)
                    {
                        if (vm.GetVirtualisationStatus != vs0)
                        {
                            identical = false;
                            break;
                        }
                    }
                    if (identical)
                    {
                        switch (vm0.GetVirtualisationStatus)
                        {
                            case VM.VirtualisationStatus.OPTIMIZED:
                                labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_NOTSUPPORTED_PLURAL;
                                break;
                            case VM.VirtualisationStatus.PV_DRIVERS_NOT_INSTALLED:
                                labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_NOTOOLS_PLURAL;
                                break;
                            case VM.VirtualisationStatus.PV_DRIVERS_OUT_OF_DATE:
                                labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_OLDTOOLS_PLURAL;
                                break;
                            default:  // VM.VirtualisationStatus.UNKNOWN
                                labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_VMS;
                                break;
                        }
                    }
                    else
                        labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_VMS;
                    linkInstallTools.Visible = InstallToolsCommand.CanExecuteAll(vms);
                }
                else if (vm0.is_a_template)
                {
                    labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_TEMPLATE;
                    linkInstallTools.Visible = false;
                }
                else
                {
                    switch (vm0.GetVirtualisationStatus)
                    {
                        case VM.VirtualisationStatus.OPTIMIZED:
                            labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_NOTSUPPORTED;
                            break;
                        case VM.VirtualisationStatus.PV_DRIVERS_NOT_INSTALLED:
                            labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_NOTOOLS;
                            break;
                        case VM.VirtualisationStatus.PV_DRIVERS_OUT_OF_DATE:
                            labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_OLDTOOLS;
                            break;
                        default:  // VM.VirtualisationStatus.UNKNOWN
                            labelDMCUnavailable.Text = Messages.DMC_UNAVAILABLE_VM;
                            break;
                    }
                    linkInstallTools.Visible = InstallToolsCommand.CanExecute(vm0);
                }
            }

            // Spinners
            FreeSpinnerRanges();
            static_min = vm0.memory_static_min;
            memorySpinnerDynMin.Initialize(Messages.DYNAMIC_MIN_AMP, ballooning ? XenAdmin.Properties.Resources.memory_dynmin_slider_small : null, vm0.memory_dynamic_min);
            memorySpinnerDynMax.Initialize(Messages.DYNAMIC_MAX_AMP, ballooning ? XenAdmin.Properties.Resources.memory_dynmax_slider_small : null, vm0.memory_dynamic_max);
            memorySpinnerFixed.Initialize("", null, vm0.memory_static_max);
            SetIncrements();
            SetSpinnerRanges();
            firstPaint = false;
        }

        public override long dynamic_min
        {
            get
            {
                System.Diagnostics.Trace.Assert(ballooning);
                return (radioOn.Checked ? memorySpinnerDynMin.Value : memorySpinnerFixed.Value);
            }
        }

        public override long dynamic_max
        {
            get
            {
                System.Diagnostics.Trace.Assert(ballooning);
                return (radioOn.Checked ? memorySpinnerDynMax.Value : memorySpinnerFixed.Value);
            }
        }

        public override long static_max
        {
            get
            {
                return (radioOn.Checked ? memorySpinnerDynMax.Value : memorySpinnerFixed.Value);
            }
        }

        private void SetIncrements()
        {
            vmShinyBar.Increment = memorySpinnerDynMin.Increment = memorySpinnerDynMax.Increment = CalcIncrement();
        }

        private void DynamicSpinners_ValueChanged(object sender, EventArgs e)
        {
            if (firstPaint)  // still initialising
                return;
            radioOn.Checked = true;
            if (sender == memorySpinnerDynMax)
            {
                // Force supported envelope
                FreeSpinnerRanges();
                long min = (long)((double)static_max * GetMemoryRatio());
                if (memorySpinnerDynMin.Value < min)
                    memorySpinnerDynMin.Initialize(Messages.DYNAMIC_MIN_AMP, XenAdmin.Properties.Resources.memory_dynmin_slider_small, min, RoundingBehaviour.Up);
                SetIncrements();
            }
            SetSpinnerRanges();
            vmShinyBar.ChangeSettings(static_min, dynamic_min, dynamic_max, static_max);
            vmShinyBar.Refresh();
        }

        private void FixedSpinner_ValueChanged(object sender, EventArgs e)
        {
            if (firstPaint)  // still initialising
                return;
            radioOff.Checked = true;
        }

        private void SetSpinnerRanges()
        {
            // Set the limit for the fixed spinner
            long maxFixed = ((maxDynMin >= 0 && maxDynMin <= MemorySpinnerMax) ? maxDynMin : MemorySpinnerMax);
            memorySpinnerFixed.SetRange(vm0.memory_static_min >= Util.BINARY_MEGA ? vm0.memory_static_min : Util.BINARY_MEGA, maxFixed);

            if (!ballooning)
                return;

            // Calculate limits for the dynamic spinners
            long maxDM = DynMinSpinnerMax;
            long minDM = DynMinSpinnerMin;
            long maxSM = StatMaxSpinnerMax;

            // Set the limits
            memorySpinnerDynMin.SetRange(minDM, maxDM);
            memorySpinnerDynMax.SetRange(dynamic_min >= Util.BINARY_MEGA ? dynamic_min : Util.BINARY_MEGA, maxSM);
            vmShinyBar.SetRanges(minDM, maxDM, dynamic_min, maxSM);
        }

        private void FreeSpinnerRanges()
        {
            memorySpinnerDynMin.SetRange(0, MemorySpinnerMax);
            memorySpinnerDynMax.SetRange(0, MemorySpinnerMax);
            memorySpinnerFixed.SetRange(0, MemorySpinnerMax);
        }

        private void vmShinyBar_SliderDragged(object sender, EventArgs e)
        {
            memorySpinnerDynMin.Initialize(Messages.DYNAMIC_MIN_AMP, XenAdmin.Properties.Resources.memory_dynmin_slider_small, vmShinyBar.Dynamic_min);
            memorySpinnerDynMax.Initialize(Messages.DYNAMIC_MAX_AMP, XenAdmin.Properties.Resources.memory_dynmax_slider_small, vmShinyBar.Dynamic_max);
            memorySpinnerDynMin.Refresh();
            memorySpinnerDynMax.Refresh();
        }

        private void InstallTools_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            if (new InstallToolsCommand(Program.MainWindow, vms).ConfirmAndExecute())
                OnInstallTools();
        }

        private void OnInstallTools()
        {
            if (InstallTools != null)
                InstallTools(this, new EventArgs());
        }
    }
}
