using EMS.WebApp.Data;

namespace EMS.WebApp.Services
{
    public class MedicalDataMaskingService : IMedicalDataMaskingService
    {
        private readonly List<string> _authorizedRoles = new List<string> { "doctor", "compounder" };
        private const string MASKED_VALUE = "*****";

        public bool ShouldMaskData(string? userRole)
        {
            if (string.IsNullOrEmpty(userRole))
                return true;

            return !_authorizedRoles.Contains(userRole.ToLower());
        }

        public string MaskValue(string? value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : MASKED_VALUE;
        }

        public T MaskObject<T>(T obj, string? userRole) where T : class
        {
            if (obj == null || !ShouldMaskData(userRole))
                return obj;

            // Handle different object types
            switch (obj)
            {
                case DoctorDiagnosisViewModel viewModel:
                    MaskDoctorDiagnosisViewModel(viewModel);
                    break;

                case PrescriptionDetailsViewModel prescriptionDetails:
                    MaskPrescriptionDetailsViewModel(prescriptionDetails);
                    break;

                case PendingApprovalViewModel pendingApproval:
                    MaskPendingApprovalViewModel(pendingApproval);
                    break;

                case List<PendingApprovalViewModel> pendingApprovals:
                    pendingApprovals.ForEach(pa => MaskPendingApprovalViewModel(pa));
                    break;

                // ======= NEW: Handle OthersDiagnosis models =======
                case OthersDiagnosisViewModel othersDiagnosisViewModel:
                    MaskOthersDiagnosisViewModel(othersDiagnosisViewModel);
                    break;

                case OthersDiagnosisDetailsViewModel othersDiagnosisDetailsViewModel:
                    MaskOthersDiagnosisDetailsViewModel(othersDiagnosisDetailsViewModel);
                    break;

                case OthersPendingApprovalViewModel othersPendingApprovalViewModel:
                    MaskOthersPendingApprovalViewModel(othersPendingApprovalViewModel);
                    break;

                case List<OthersPendingApprovalViewModel> othersPendingApprovals:
                    othersPendingApprovals.ForEach(pa => MaskOthersPendingApprovalViewModel(pa));
                    break;
            }

            return obj;
        }

        private void MaskDoctorDiagnosisViewModel(DoctorDiagnosisViewModel model)
        {
            model.BloodPressure = MaskValue(model.BloodPressure);
            model.Pulse = MaskValue(model.Pulse);
            model.Temperature = MaskValue(model.Temperature);
        }

        private void MaskPrescriptionDetailsViewModel(PrescriptionDetailsViewModel model)
        {
            model.BloodPressure = MaskValue(model.BloodPressure);
            model.Pulse = MaskValue(model.Pulse);
            model.Temperature = MaskValue(model.Temperature);

            // Mask medicine names
            foreach (var medicine in model.Medicines)
            {
                medicine.MedicineName = MaskValue(medicine.MedicineName);
            }
        }

        private void MaskPendingApprovalViewModel(PendingApprovalViewModel model)
        {
            model.BloodPressure = MaskValue(model.BloodPressure);
            model.Pulse = MaskValue(model.Pulse);
            model.Temperature = MaskValue(model.Temperature);

            // Mask medicine names
            foreach (var medicine in model.Medicines)
            {
                medicine.MedicineName = MaskValue(medicine.MedicineName);
            }
        }

        // ======= NEW: OthersDiagnosis masking methods =======

        private void MaskOthersDiagnosisViewModel(OthersDiagnosisViewModel model)
        {
            model.BloodPressure = MaskValue(model.BloodPressure);
            model.PulseRate = MaskValue(model.PulseRate);
            model.Sugar = MaskValue(model.Sugar);

            // Mask medicine names in prescription medicines
            if (model.PrescriptionMedicines?.Any() == true)
            {
                foreach (var medicine in model.PrescriptionMedicines)
                {
                    medicine.MedicineName = MaskValue(medicine.MedicineName);
                }
            }
        }

        private void MaskOthersDiagnosisDetailsViewModel(OthersDiagnosisDetailsViewModel model)
        {
            model.BloodPressure = MaskValue(model.BloodPressure);
            model.PulseRate = MaskValue(model.PulseRate);
            model.Sugar = MaskValue(model.Sugar);

            // Mask medicine names
            if (model.Medicines?.Any() == true)
            {
                foreach (var medicine in model.Medicines)
                {
                    medicine.MedicineName = MaskValue(medicine.MedicineName);
                }
            }
        }

        private void MaskOthersPendingApprovalViewModel(OthersPendingApprovalViewModel model)
        {
            model.BloodPressure = MaskValue(model.BloodPressure);
            model.PulseRate = MaskValue(model.PulseRate);
            model.Sugar = MaskValue(model.Sugar);

            // Mask medicine names
            if (model.Medicines?.Any() == true)
            {
                foreach (var medicine in model.Medicines)
                {
                    medicine.MedicineName = MaskValue(medicine.MedicineName);
                }
            }
        }
    }
}