using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Microsoft.VisualBasic;
using System.Text.RegularExpressions;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
// [assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, System.Windows.Window window, ScriptEnvironment environment*/)
        {
            Patient p = context.Patient;

            ExternalPlanSetup firstPlan = PlanSelector(p)[0];
            ExternalPlanSetup secondPlan = PlanSelector(p)[0];

            foreach (Beam firstBeam in firstPlan.Beams)
            {
                foreach (Beam secondBeam in secondPlan.Beams)
                {
                    if (firstBeam.Id == secondBeam.Id)
                    {
                        BeamPropertyDiffer(firstBeam, secondBeam);
                        LeafDiffer(firstBeam, secondBeam);

                        // calc models identical?
                        MessageBox.Show(firstPlan.GetCalculationModel(CalculationType.PhotonVolumeDose).ToString());
                        MessageBox.Show(secondPlan.GetCalculationModel(CalculationType.PhotonVolumeDose).ToString());

                        // calc model options identical?
                        MessageBox.Show(StringifyDict(firstPlan.GetCalculationOptions(firstPlan.GetCalculationModel(CalculationType.PhotonVolumeDose))) +
                                       "\n" +
                                        StringifyDict(secondPlan.GetCalculationOptions(secondPlan.GetCalculationModel(CalculationType.PhotonVolumeDose))));

                        // doses identical?
                        BeamDose firstBeamRelativeDose = firstBeam.Dose;
                        BeamDose secondBeamRelativeDose = secondBeam.Dose;
                        MessageBox.Show("doses same? " + firstBeamRelativeDose.Equals(secondBeamRelativeDose).ToString());

                        // normalization methods identical?
                        string firstBeamNormalizationMethod = firstBeam.NormalizationMethod;
                        string secondBeamNormalizationMethod = secondBeam.NormalizationMethod;
                        MessageBox.Show(firstBeamNormalizationMethod + "\n" + secondBeamNormalizationMethod);

                        // normalization values identical?
                        MessageBox.Show(firstPlan.PlanNormalizationValue.ToString() + "\n" + secondPlan.PlanNormalizationValue.ToString());

                        // meterset units and values identical?
                        MessageBox.Show("First beam meterset unit: " + firstBeam.Meterset.Unit.ToString() +
                                        "\nSecond beam meterset unit: " + secondBeam.Meterset.Unit.ToString() +
                                        "\nFirst beam meterset value: " + firstBeam.Meterset.Value.ToString() +
                                        "\nSecond beam meterset value: " + secondBeam.Meterset.Value.ToString());
                    }
                }
            }
        }

        bool AreArraysEqual(Single[,] array1, Single[,] array2)
        {
            int rows = array1.GetLength(0);
            int cols = array1.GetLength(1);

            if (rows != array2.GetLength(0) || cols != array2.GetLength(1))
                return false; // Arrays have different dimensions

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (array1[i, j] != array2[i, j])
                        return false;
                }
            }

            return true; // Arrays are identical
        }

        Single[,] CalculateDifferenceMatrix(Single[,] array1, Single[,] array2)
        {
            int rows = array1.GetLength(0);
            int cols = array1.GetLength(1);

            if (rows != array2.GetLength(0) || cols != array2.GetLength(1))
                throw new ArgumentException("Arrays must have the same dimensions");

            Single[,] differenceMatrix = new Single[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    differenceMatrix[i, j] = array1[i, j] - array2[i, j];
                }
            }

            return differenceMatrix;
        }

        public void ShowDifferenceMatrix(Single[,] differenceMatrix)
        {
            StringBuilder matrixString = new StringBuilder();
            int rows = differenceMatrix.GetLength(0);
            int cols = differenceMatrix.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    matrixString.Append(differenceMatrix[i, j].ToString());
                    if (j < cols - 1)
                        matrixString.Append("\t"); // Tab separator for columns
                }
                matrixString.AppendLine(); // Newline separator for rows
            }

            MessageBox.Show(matrixString.ToString());
        }

        /// <summary>
        /// Checks for differences between two beams.
        /// </summary>
        /// <param name="firstBeam"></param>
        /// <param name="secondBeam"></param>
        public void BeamPropertyDiffer(Beam firstBeam, Beam secondBeam)
        {
            foreach (PropertyInfo property in firstBeam.GetType().GetProperties())
            {
                try
                {
                    if (property.GetValue(firstBeam).ToString() != property.GetValue(secondBeam).ToString())
                    {
                        MessageBox.Show("Discrepancy for property " +
                                         property.Name +
                                         ":\nFirst beam: " +
                                         property.GetValue(firstBeam).ToString() +
                                         "\nSecond beam: " +
                                         property.GetValue(secondBeam).ToString());
                    }
                }
                catch (Exception)
                {

                    MessageBox.Show("No instance of " + property.Name);
                }
            }
        }

        /// <summary>
        /// For two beams, see if their leaf patterns are identical. This assumes the same number of control points for each beam.
        /// </summary>
        /// <param name="firstBeam"></param>
        /// <param name="secondBeam"></param>
        public void LeafDiffer(Beam firstBeam, Beam secondBeam)
        {
            bool incongruityFlag = false;
            ControlPointCollection firstBeamCPs = firstBeam.ControlPoints;
            ControlPointCollection secondBeamCPs = secondBeam.ControlPoints;
            foreach (ControlPoint fbcp in firstBeamCPs)
            {
                int fbcpindex = fbcp.Index;
                ControlPoint sbcp = secondBeamCPs[fbcpindex];
                if (!AreArraysEqual(fbcp.LeafPositions, sbcp.LeafPositions))
                {
                    incongruityFlag = true;
                    MessageBox.Show("Incongruity at CP" + fbcpindex);
                    ShowDifferenceMatrix(CalculateDifferenceMatrix(fbcp.LeafPositions, sbcp.LeafPositions));
                }
            }
            if (!incongruityFlag) { MessageBox.Show("No leaf position incongruities detected."); }
        }

        public static string StringifyDict(Dictionary<string, string> dict)
        {
            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, string> kvp in dict)
            {
                sb.AppendLine("Key = " + kvp.Key.ToString() + ", Value = " + kvp.Value.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Get user input to select plans.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static List<ExternalPlanSetup> PlanSelector(Patient p)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Please select plans enumerated below in the format \"#.#\", space-delimited, no trailing whitespace.");
            int courseCounter = 1;
            foreach (Course course in p.Courses)
            {
                int planCounter = 1;
                sb.AppendLine($"{courseCounter} {course.Id} {course.ClinicalStatus}");
                foreach (PlanSetup plan in course.PlanSetups)
                {
                    sb.AppendLine($"- {courseCounter}.{planCounter} {plan.Id}");
                    planCounter++;
                }
                courseCounter++;
            }

            string selectedPlansString;
            string pattern = @"^(\d+\.\d+)( \d+\.\d+)*$";
            List<ExternalPlanSetup> selectedPlans = new List<ExternalPlanSetup>();
            while (true)
            {
                selectedPlansString = Interaction.InputBox(sb.ToString(), "Select Plans");
                if (Regex.IsMatch(selectedPlansString, pattern))
                {
                    bool failFlag = false;
                    foreach (var entry in selectedPlansString.Split(' '))
                    {
                        var indices = entry.Split('.');
                        int courseIndex = Int32.Parse(indices[0]);
                        int planIndex = Int32.Parse(indices[1]);
                        if (courseIndex - 1 >= 0 && courseIndex - 1 < p.Courses.Count() &&
                            planIndex - 1 >= 0 && planIndex - 1 < p.Courses.ElementAt(courseIndex - 1).PlanSetups.Count())
                        {
                            Course selectedCourse = p.Courses.ElementAt(courseIndex - 1);
                            ExternalPlanSetup selectedPlan = selectedCourse.ExternalPlanSetups.ElementAt(planIndex - 1);
                            selectedPlans.Add(selectedPlan);
                        }
                        else
                        {
                            MessageBox.Show($"Index out of bounds for entry {entry}. Please try again.");
                            failFlag = true;
                        }
                    }
                    if (failFlag) { continue; }
                    break;
                }
                else if (selectedPlansString == "")
                {
                    return selectedPlans;
                }
                else
                {
                    MessageBox.Show("Improperly formatted selection. Ensure that all plan selections are of the form #.# and separated only by spaces.");
                }
            }
            return selectedPlans;
        }
    }
}
