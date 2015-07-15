﻿<%@ Control Language="C#" AutoEventWireup="true" CodeFile="Metrics.ascx.cs" Inherits="RockWeb.Plugins.cc_newspring.Metrics.Metrics" %>

<div class="col-md-<%= metricWidth.Value %>">

    <asp:UpdatePanel ID="pnlContent" runat="server">
        <ContentTemplate>

            <asp:HiddenField ID="metricWidth" runat="server" />
            <asp:HiddenField ID="metricClass" runat="server" />
            <asp:HiddenField ID="metricDisplay" runat="server" />
            <asp:HiddenField ID="metricNumber" runat="server" />
            <asp:HiddenField ID="metricTitle" runat="server" />

            <% if ( metricDisplay.Value.Equals( "Text" ) )
               { %>
            <div class="panel panel-block">
                <div class="panel-heading clearfix">
                    <h1 class="panel-title pull-left"><%= metricTitle.Value %></h1>
                </div>
                <div class="panel-body">
                    <h1 class="flush"><%= metricNumber.Value %><% if ( metricClass.Value != "" )
                                                                  { %> <i class="fa fa-fw <%= metricClass.Value %> pull-right"></i><% } %></h1>
                </div>
            </div>
            <% }
               else if ( metricDisplay.Value.Equals( "Line" ) )
               { %>

            <asp:HiddenField ID="currentYear" runat="server" />
            <asp:HiddenField ID="previousYear" runat="server" />
            <asp:HiddenField ID="metricLabels" runat="server" />
            <asp:HiddenField ID="metricDataPointsCurrent" runat="server" />
            <asp:HiddenField ID="metricDataPointsPrevious" runat="server" />

            <script>
                var randomScalingFactor = function () { return Math.round(Math.random() * (36000 - 45000) + 36000) };

                var lineOptions = {
                    responsive: true,
                    scaleFontSize: 16,
                    tooltipFontSize: 16,
                    bezierCurve: false,
                    datasetStrokeWidth: 3,
                    pointDotRadius: 6,
                    pointDotStrokeWidth: 3,
                }

                var overallAttendanceData = {
                    labels: [<%= metricLabels.Value %>],
                    datasets: [
                        {
                            label: "<%= currentYear.Value %>",
                            fillColor: "rgba(28,104,62,0)",
                            strokeColor: "rgba(28,104,62,1)",
                            pointColor: "rgba(28,104,62,1)",
                            pointStrokeColor: "#fff",
                            data: [<%= metricDataPointsCurrent.Value %>]
                        },
                        {
                            label: "<%= previousYear.Value %>",
                            fillColor: "rgba(89,161,46,0)",
                            strokeColor: "rgba(89,161,46,1)",
                            pointColor: "rgba(89,161,46,1)",
                            pointStrokeColor: "#fff",
                            data: [<%= metricDataPointsPrevious.Value %>]
                        }
                    ]
                }

                    window.onload = function () {
                        var overallAttendance = document.getElementById("attendanceChart").getContext("2d");

                        window.attendanceChart = new Chart(overallAttendance).Line(overallAttendanceData, lineOptions);
                    }
            </script>

            <div class="panel panel-block panel-chart">
                <div class="panel-heading clearfix">
                    <h1 class="panel-title pull-left"><%= metricTitle.Value %></h1>
                </div>
                <div class="panel-body">
                    <canvas id="attendanceChart"></canvas>
                </div>
            </div>

            <% }
               else if ( metricDisplay.Value.Equals( "Donut" ) )
               { %>

            <asp:HiddenField ID="metricLabel" runat="server" />
            <asp:HiddenField ID="metricValue" runat="server" />
            <asp:HiddenField ID="metricCount" runat="server" />
            <asp:HiddenField ID="metricBlockId" runat="server" />
            <asp:HiddenField ID="metricItemName" runat="server" />

            <script>

                var pieOptions = {
                    animation: false,
                    responsive: true,
                    scaleFontSize: 16,
                    tooltipFontSize: 16,
                    bezierCurve: false,
                    datasetStrokeWidth: 3,
                    pointDotRadius: 6,
                    pointDotStrokeWidth: 3,
                }

                window.onload = function () {
                    var <%= metricBlockId.Value %> = document.getElementById("<%= metricBlockId.Value %>Chart").getContext("2d");
                    window.<%= metricBlockId.Value %>Chart = new Chart(<%= metricBlockId.Value %>).Doughnut(<%= metricBlockValues %>, pieOptions);
                    }
            </script>

            <div class="panel panel-block panel-chart">
                <div class="panel-heading clearfix">
                    <h1 class="panel-title pull-left"><%= metricTitle.Value %></h1>
                </div>
                <div class="panel-body">
                    <canvas id="<%= metricBlockId.Value %>Chart"></canvas>
                </div>
            </div>

            <% } %>

            <Rock:NotificationBox ID="churchMetricWarning" runat="server" NotificationBoxType="Warning"
                Text="Please select a metric in the block settings." />
        </ContentTemplate>
    </asp:UpdatePanel>
</div>