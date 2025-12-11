using EMS.WebApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace EMS.WebApp.Services
{
    public class ExpiredMedicineRepository : IExpiredMedicineRepository
    {
        private readonly ApplicationDbContext _db;

        public ExpiredMedicineRepository(ApplicationDbContext db) => _db = db;

        public async Task<string?> GetPlantCodeByIdAsync(int plantId)
        {
            try
            {
                var plant = await _db.org_plants.FirstOrDefaultAsync(p => p.plant_id == plantId);
                return plant?.plant_code;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting plant code for plant ID {plantId}: {ex.Message}");
                return null;
            }
        }

        private IQueryable<ExpiredMedicine> ApplyRoleBasedFilter(IQueryable<ExpiredMedicine> query, string? userRole)
        {
            if (string.IsNullOrEmpty(userRole))
                return query;

            var roleLower = userRole.ToLower();

            if (roleLower.Contains("store"))
            {
                return query.Where(e => e.SourceType == "Store");
            }
            else if (roleLower.Contains("compounder"))
            {
                return query.Where(e => e.SourceType == "Compounder");
            }
            else if (roleLower.Contains("doctor"))
            {
                return query; // Doctors can see both
            }

            return query; // Default: show all if role doesn't match known patterns
        }
        /// <summary>
        /// Applies BCM plant-specific creator filtering for Compounder users
        /// For BCM plant + Compounder role: Filter by who created the source CompounderIndent
        /// </summary>
        private async Task<IQueryable<ExpiredMedicine>> ApplyBcmFilterAsync(
            IQueryable<ExpiredMedicine> query,
            int? userPlantId,
            string? userRole,
            string? currentUser)
        {
            // Only apply BCM filtering if we have all required parameters
            if (!userPlantId.HasValue || string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(currentUser))
                return query;

            // Check if user is a Compounder (doctors see all, store users only see store)
            var roleLower = userRole.ToLower();
            if (!roleLower.Contains("compounder"))
                return query; // Non-compounders don't get this BCM filtering

            // Check if plant is BCM
            var plantCode = await GetPlantCodeByIdAsync(userPlantId.Value);
            if (plantCode?.ToUpper() != "BCM")
                return query; // Non-BCM plants: no creator filtering

            // BCM plant + Compounder role: Filter by CompounderIndent.CreatedBy
            System.Diagnostics.Debug.WriteLine($"🔒 BCM Plant + Compounder: Filtering expired medicines by indent creator: {currentUser}");

            // Get list of CompounderIndentItemIds where the parent indent was created by this user
            var userIndentItemIds = await _db.CompounderIndentItems
                .Include(i => i.CompounderIndent)
                .Where(i => i.CompounderIndent.CreatedBy == currentUser &&
                            i.CompounderIndent.plant_id == userPlantId.Value)
                .Select(i => i.IndentItemId)
                .ToListAsync();

            System.Diagnostics.Debug.WriteLine($"🔍 Found {userIndentItemIds.Count} indent items created by {currentUser}");

            // Filter expired medicines to only those from user's indents
            return query.Where(e => e.CompounderIndentItemId.HasValue &&
                                   userIndentItemIds.Contains(e.CompounderIndentItemId.Value));
        }
        // Helper method to safely load navigation properties
        private async Task LoadNavigationPropertiesAsync(ExpiredMedicine item)
        {
            // Load OrgPlant (should always exist)
            await _db.Entry(item).Reference(e => e.OrgPlant).LoadAsync();

            // Load source-specific navigation properties
            if (item.SourceType == "Store" && item.StoreIndentItemId.HasValue)
            {
                // Check if StoreIndentItem exists before loading
                var storeItemExists = await _db.StoreIndentItems
                    .AnyAsync(si => si.IndentItemId == item.StoreIndentItemId.Value);

                if (storeItemExists)
                {
                    await _db.Entry(item).Reference(e => e.StoreIndentItem).LoadAsync();

                    if (item.StoreIndentItem != null)
                    {
                        await _db.Entry(item.StoreIndentItem).Reference(si => si.MedMaster).LoadAsync();
                        await _db.Entry(item.StoreIndentItem).Reference(si => si.StoreIndent).LoadAsync();
                    }
                }
            }
            else if (item.SourceType == "Compounder" && item.CompounderIndentItemId.HasValue)
            {
                // Check if CompounderIndentItem exists before loading
                var compounderItemExists = await _db.CompounderIndentItems
                    .AnyAsync(ci => ci.IndentItemId == item.CompounderIndentItemId.Value);

                if (compounderItemExists)
                {
                    await _db.Entry(item).Reference(e => e.CompounderIndentItem).LoadAsync();

                    if (item.CompounderIndentItem != null)
                    {
                        await _db.Entry(item.CompounderIndentItem).Reference(ci => ci.MedMaster).LoadAsync();
                        await _db.Entry(item.CompounderIndentItem).Reference(ci => ci.CompounderIndent).LoadAsync();
                    }
                }
            }
        }

        // ======= FIXED: Basic CRUD operations WITH PLANT FILTERING AND ROLE-BASED ACCESS =======
        //public async Task<ExpiredMedicine?> GetByIdAsync(int id, int? userPlantId = null, string? userRole = null)
        //{
        //    var query = _db.ExpiredMedicines.AsQueryable();

        //    // Plant filtering - handle nullable PlantId
        //    if (userPlantId.HasValue)
        //    {
        //        query = query.Where(e => e.PlantId.HasValue && e.PlantId.Value == userPlantId.Value);
        //    }

        //    // Role-based filtering
        //    query = ApplyRoleBasedFilter(query, userRole);

        //    return await query.FirstOrDefaultAsync(e => e.ExpiredMedicineId == id);
        //}
        public async Task<ExpiredMedicine?> GetByIdAsync(int id, int? userPlantId = null, string? userRole = null, string? currentUser = null)
        {
            var query = _db.ExpiredMedicines.AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId.HasValue && e.PlantId.Value == userPlantId.Value);
            }

            // Role-based source type filtering
            query = ApplyRoleBasedFilter(query, userRole);

            // BCM plant-specific creator filtering
            query = await ApplyBcmFilterAsync(query, userPlantId, userRole, currentUser);

            return await query.FirstOrDefaultAsync(e => e.ExpiredMedicineId == id);
        }
        public async Task<ExpiredMedicine?> GetByIdWithDetailsAsync(int id, int? userPlantId = null, string? userRole = null)
        {
            var query = _db.ExpiredMedicines.AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            var item = await query.FirstOrDefaultAsync(e => e.ExpiredMedicineId == id);

            if (item == null) return null;

            // Load navigation properties safely
            await LoadNavigationPropertiesAsync(item);

            return item;
        }

        //public async Task<IEnumerable<ExpiredMedicine>> ListAsync(int? userPlantId = null, string? userRole = null)
        //{
        //    var query = _db.ExpiredMedicines.AsQueryable();

        //    // Plant filtering
        //    if (userPlantId.HasValue)
        //    {
        //        query = query.Where(e => e.PlantId == userPlantId.Value);
        //    }

        //    // Role-based filtering
        //    query = ApplyRoleBasedFilter(query, userRole);

        //    var results = await query
        //        .OrderByDescending(e => e.DetectedDate)
        //        .ThenBy(e => e.MedicineName)
        //        .ToListAsync();

        //    // Load navigation properties for each item
        //    foreach (var item in results)
        //    {
        //        await LoadNavigationPropertiesAsync(item);
        //    }

        //    return results;
        //}
        public async Task<IEnumerable<ExpiredMedicine>> ListAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null)
        {
            var query = _db.ExpiredMedicines.AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based source type filtering (Store vs Compounder)
            query = ApplyRoleBasedFilter(query, userRole);

            // BCM plant-specific creator filtering
            query = await ApplyBcmFilterAsync(query, userPlantId, userRole, currentUser);

            var results = await query
                .OrderByDescending(e => e.DetectedDate)
                .ThenBy(e => e.MedicineName)
                .ToListAsync();

            // Load navigation properties for each item
            foreach (var item in results)
            {
                await LoadNavigationPropertiesAsync(item);
            }

            return results;
        }

        //public async Task<IEnumerable<ExpiredMedicine>> ListPendingDisposalAsync(int? userPlantId = null, string? userRole = null)
        //{
        //    var query = _db.ExpiredMedicines
        //        .Where(e => e.Status == "Pending Disposal")
        //        .AsQueryable();

        //    // Plant filtering
        //    if (userPlantId.HasValue)
        //    {
        //        query = query.Where(e => e.PlantId == userPlantId.Value);
        //    }

        //    // Role-based filtering
        //    query = ApplyRoleBasedFilter(query, userRole);

        //    var results = await query
        //        .OrderBy(e => e.ExpiryDate)
        //        .ThenBy(e => e.MedicineName)
        //        .ToListAsync();

        //    // Load navigation properties for each item
        //    foreach (var item in results)
        //    {
        //        await LoadNavigationPropertiesAsync(item);
        //    }

        //    return results;
        //}
        public async Task<IEnumerable<ExpiredMedicine>> ListPendingDisposalAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null)
        {
            var query = _db.ExpiredMedicines
                .Where(e => e.Status == "Pending Disposal")
                .AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based source type filtering
            query = ApplyRoleBasedFilter(query, userRole);

            // BCM plant-specific creator filtering
            query = await ApplyBcmFilterAsync(query, userPlantId, userRole, currentUser);

            var results = await query
                .OrderBy(e => e.ExpiryDate)
                .ThenBy(e => e.MedicineName)
                .ToListAsync();

            // Load navigation properties for each item
            foreach (var item in results)
            {
                await LoadNavigationPropertiesAsync(item);
            }

            return results;
        }
        //public async Task<IEnumerable<ExpiredMedicine>> ListDisposedAsync(DateTime? fromDate = null, DateTime? toDate = null, int? userPlantId = null, string? userRole = null)
        //{
        //    var query = _db.ExpiredMedicines
        //        .Where(e => e.Status == "Issued to Biomedical Waste")
        //        .AsQueryable();

        //    // Plant filtering
        //    if (userPlantId.HasValue)
        //    {
        //        query = query.Where(e => e.PlantId == userPlantId.Value);
        //    }

        //    // Role-based filtering
        //    query = ApplyRoleBasedFilter(query, userRole);

        //    // Date filtering
        //    if (fromDate.HasValue)
        //    {
        //        query = query.Where(e => e.BiomedicalWasteIssuedDate >= fromDate.Value.Date);
        //    }

        //    if (toDate.HasValue)
        //    {
        //        var endDate = toDate.Value.Date.AddDays(1);
        //        query = query.Where(e => e.BiomedicalWasteIssuedDate < endDate);
        //    }

        //    var results = await query
        //        .OrderByDescending(e => e.BiomedicalWasteIssuedDate)
        //        .ThenBy(e => e.MedicineName)
        //        .ToListAsync();

        //    // Load navigation properties for each item
        //    foreach (var item in results)
        //    {
        //        await LoadNavigationPropertiesAsync(item);
        //    }

        //    return results;
        //}
        public async Task<IEnumerable<ExpiredMedicine>> ListDisposedAsync(DateTime? fromDate = null, DateTime? toDate = null, int? userPlantId = null, string? userRole = null, string? currentUser = null)
        {
            var query = _db.ExpiredMedicines
                .Where(e => e.Status == "Issued to Biomedical Waste")
                .AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based source type filtering
            query = ApplyRoleBasedFilter(query, userRole);

            // BCM plant-specific creator filtering
            query = await ApplyBcmFilterAsync(query, userPlantId, userRole, currentUser);

            // Date filtering
            if (fromDate.HasValue)
            {
                query = query.Where(e => e.BiomedicalWasteIssuedDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(e => e.BiomedicalWasteIssuedDate < endDate);
            }

            var results = await query
                .OrderByDescending(e => e.BiomedicalWasteIssuedDate)
                .ThenBy(e => e.MedicineName)
                .ToListAsync();

            // Load navigation properties for each item
            foreach (var item in results)
            {
                await LoadNavigationPropertiesAsync(item);
            }

            return results;
        }

        public async Task AddAsync(ExpiredMedicine entity)
        {
            _db.ExpiredMedicines.Add(entity);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(ExpiredMedicine entity)
        {
            _db.ExpiredMedicines.Update(entity);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int? userPlantId = null, string? userRole = null)
        {
            var query = _db.ExpiredMedicines.AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            var entity = await query.FirstOrDefaultAsync(e => e.ExpiredMedicineId == id);
            if (entity != null)
            {
                _db.ExpiredMedicines.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        // ======= FIXED: Business logic methods WITH ROLE-BASED ACCESS =======
        public async Task<IEnumerable<ExpiredMedicine>> GetByStatusAsync(string status, int? userPlantId = null, string? userRole = null)
        {
            var query = _db.ExpiredMedicines
                .Where(e => e.Status == status)
                .AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            var results = await query
                .OrderByDescending(e => e.DetectedDate)
                .ToListAsync();

            // Load navigation properties for each item
            foreach (var item in results)
            {
                await LoadNavigationPropertiesAsync(item);
            }

            return results;
        }

        public async Task<IEnumerable<ExpiredMedicine>> GetByPriorityLevelAsync(string priority, int? userPlantId = null, string? userRole = null)
        {
            var query = _db.ExpiredMedicines
                .Where(e => e.Status == "Pending Disposal")
                .AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            var today = DateTime.Today;

            query = priority.ToLower() switch
            {
                "low" => query.Where(e => EF.Functions.DateDiffDay(e.ExpiryDate, today) <= 30),
                "medium" => query.Where(e => EF.Functions.DateDiffDay(e.ExpiryDate, today) > 30 &&
                                           EF.Functions.DateDiffDay(e.ExpiryDate, today) <= 90),
                "high" => query.Where(e => EF.Functions.DateDiffDay(e.ExpiryDate, today) > 90),
                _ => query
            };

            var results = await query.OrderBy(e => e.ExpiryDate)
                             .ToListAsync();

            // Load navigation properties for each item
            foreach (var item in results)
            {
                await LoadNavigationPropertiesAsync(item);
            }

            return results;
        }

        public async Task<IEnumerable<ExpiredMedicine>> GetCriticalExpiredMedicinesAsync(int? userPlantId = null, string? userRole = null)
        {
            var today = DateTime.Today;
            var query = _db.ExpiredMedicines
                .Where(e => e.Status == "Pending Disposal" &&
                           EF.Functions.DateDiffDay(e.ExpiryDate, today) > 90)
                .AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            var results = await query
                .OrderBy(e => e.ExpiryDate)
                .ToListAsync();

            // Load navigation properties for each item
            foreach (var item in results)
            {
                await LoadNavigationPropertiesAsync(item);
            }

            return results;
        }

        // ======= UPDATED: Check if already tracked WITH SOURCE TYPE SUPPORT =======
        public async Task<bool> IsAlreadyTrackedAsync(int? compounderIndentItemId = null, int? storeIndentItemId = null, string? batchNo = null, DateTime? expiryDate = null, int? userPlantId = null)
        {
            var query = _db.ExpiredMedicines.AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            if (compounderIndentItemId.HasValue)
            {
                query = query.Where(e => e.CompounderIndentItemId == compounderIndentItemId.Value);
            }

            if (storeIndentItemId.HasValue)
            {
                query = query.Where(e => e.StoreIndentItemId == storeIndentItemId.Value);
            }

            if (!string.IsNullOrEmpty(batchNo))
            {
                query = query.Where(e => e.BatchNumber == batchNo);
            }

            if (expiryDate.HasValue)
            {
                query = query.Where(e => e.ExpiryDate.Date == expiryDate.Value.Date);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> IsCompounderItemAlreadyTrackedAsync(int compounderIndentItemId, string batchNo, DateTime expiryDate, int? userPlantId = null)
        {
            return await IsAlreadyTrackedAsync(compounderIndentItemId: compounderIndentItemId, batchNo: batchNo, expiryDate: expiryDate, userPlantId: userPlantId);
        }

        public async Task<bool> IsStoreItemAlreadyTrackedAsync(int storeIndentItemId, string batchNo, DateTime expiryDate, int? userPlantId = null)
        {
            return await IsAlreadyTrackedAsync(storeIndentItemId: storeIndentItemId, batchNo: batchNo, expiryDate: expiryDate, userPlantId: userPlantId);
        }

        // ======= UPDATED: Biomedical waste operations WITH ROLE-BASED ACCESS =======
        public async Task IssueToBiomedicalWasteAsync(int expiredMedicineId, string issuedBy, int? userPlantId = null, string? userRole = null, string? remarks = null)
        {
            // Initialize the query - THIS WAS MISSING IN MY PREVIOUS RESPONSE
            var query = _db.ExpiredMedicines.AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            // Now this line will work because 'query' is defined above
            var expiredMedicine = await query.FirstOrDefaultAsync(e => e.ExpiredMedicineId == expiredMedicineId);

            if (expiredMedicine != null && expiredMedicine.Status == "Pending Disposal")
            {
                // Update expired medicine status
                expiredMedicine.Status = "Issued to Biomedical Waste";
                expiredMedicine.BiomedicalWasteIssuedDate = DateTime.Now;
                expiredMedicine.BiomedicalWasteIssuedBy = issuedBy;

                // Reduce quantity from the appropriate batch table (RENAMED METHODS)
                if (expiredMedicine.SourceType == "Compounder")
                {
                    await ReduceCompounderBatchQuantityAsync(expiredMedicine, userPlantId, issuedBy);
                }
                else if (expiredMedicine.SourceType == "Store")
                {
                    await ReduceStoreBatchQuantityAsync(expiredMedicine, userPlantId, issuedBy);
                }

                await _db.SaveChangesAsync();
            }
        }
        public async Task BulkIssueToBiomedicalWasteAsync(List<int> expiredMedicineIds, string issuedBy, int? userPlantId = null, string? userRole = null, string? remarks = null)
        {
            // Initialize the query
            var query = _db.ExpiredMedicines
                .Where(e => expiredMedicineIds.Contains(e.ExpiredMedicineId) &&
                           e.Status == "Pending Disposal")
                .AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            var expiredMedicines = await query.ToListAsync();
            var issuedDate = DateTime.Now;

            foreach (var expiredMedicine in expiredMedicines)
            {
                // Update expired medicine status
                expiredMedicine.Status = "Issued to Biomedical Waste";
                expiredMedicine.BiomedicalWasteIssuedDate = issuedDate;
                expiredMedicine.BiomedicalWasteIssuedBy = issuedBy;

                // Reduce quantity from the appropriate batch table (RENAMED METHODS)
                if (expiredMedicine.SourceType == "Compounder")
                {
                    await ReduceCompounderBatchQuantityAsync(expiredMedicine, userPlantId, issuedBy);
                }
                else if (expiredMedicine.SourceType == "Store")
                {
                    await ReduceStoreBatchQuantityAsync(expiredMedicine, userPlantId, issuedBy);
                }
            }

            await _db.SaveChangesAsync();
        }
        // ======= HELPER METHODS for batch removal =======
        private async Task ReduceCompounderBatchQuantityAsync(ExpiredMedicine expiredMedicine, int? userPlantId = null, string? issuedBy = null)
        {
            try
            {
                var batchQuery = _db.Set<CompounderIndentBatch>()
                    .Where(b => b.IndentItemId == expiredMedicine.CompounderIndentItemId &&
                               b.BatchNo == expiredMedicine.BatchNumber &&
                               b.ExpiryDate.Date == expiredMedicine.ExpiryDate.Date)
                    .AsQueryable();

                // Plant filtering for batch removal
                if (userPlantId.HasValue)
                {
                    batchQuery = batchQuery.Where(b => _db.CompounderIndentItems
                        .Any(i => i.IndentItemId == b.IndentItemId &&
                                 _db.CompounderIndents.Any(ci => ci.IndentId == i.IndentId && ci.plant_id == userPlantId.Value)));
                }

                var batchToUpdate = await batchQuery.FirstOrDefaultAsync();

                if (batchToUpdate != null)
                {
                    // CHANGED: Reduce quantity instead of removing the record
                    var quantityToReduce = expiredMedicine.QuantityExpired ?? 0;

                    
                    // COMMENTED OUT: Don't reduce stock
                    // Ensure we don't go below zero
                    //batchToUpdate.AvailableStock = Math.Max(0, batchToUpdate.AvailableStock - quantityToReduce);

                    // Optional: Add disposal tracking fields to batch table if needed
                    batchToUpdate.LastDisposalDate = DateTime.Now;
                    batchToUpdate.LastDisposalBy = issuedBy;
                    batchToUpdate.TotalDisposed = (batchToUpdate.TotalDisposed) + quantityToReduce;

                    // Update the record instead of removing it
                    _db.Set<CompounderIndentBatch>().Update(batchToUpdate);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating compounder batch: {ex.Message}");
            }
        }
        private async Task ReduceStoreBatchQuantityAsync(ExpiredMedicine expiredMedicine, int? userPlantId = null, string? issuedBy = null)
        {
            try
            {
                var batchQuery = _db.Set<StoreIndentBatch>()
                    .Where(b => b.IndentItemId == expiredMedicine.StoreIndentItemId &&
                               b.BatchNo == expiredMedicine.BatchNumber &&
                               b.ExpiryDate.Date == expiredMedicine.ExpiryDate.Date)
                    .AsQueryable();

                // Plant filtering for batch removal
                if (userPlantId.HasValue)
                {
                    batchQuery = batchQuery.Where(b => _db.StoreIndentItems
                        .Any(i => i.IndentItemId == b.IndentItemId &&
                                 _db.StoreIndents.Any(si => si.IndentId == i.IndentId && si.PlantId == userPlantId.Value)));
                }

                var batchToUpdate = await batchQuery.FirstOrDefaultAsync();

                if (batchToUpdate != null)
                {
                    // CHANGED: Reduce quantity instead of removing the record
                    var quantityToReduce = expiredMedicine.QuantityExpired ?? 0;

                    // COMMENTED OUT: Don't reduce stock
                    // Ensure we don't go below zero
                    //batchToUpdate.AvailableStock = Math.Max(0, batchToUpdate.AvailableStock - quantityToReduce);

                    // Optional: Add disposal tracking fields to batch table if needed
                     batchToUpdate.LastDisposalDate = DateTime.Now;
                     batchToUpdate.LastDisposalBy = issuedBy;
                     batchToUpdate.TotalDisposed = (batchToUpdate.TotalDisposed) + quantityToReduce;

                    // Update the record instead of removing it
                    _db.Set<StoreIndentBatch>().Update(batchToUpdate);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating store batch: {ex.Message}");
            }
        }

        // ======= UPDATED: DetectNewExpiredMedicinesAsync WITH DUAL SOURCE SUPPORT =======
        public async Task<List<ExpiredMedicine>> DetectNewExpiredMedicinesAsync(string detectedBy, int? userPlantId = null, string? sourceType = null)
        {
            var newExpiredMedicines = new List<ExpiredMedicine>();

            if (sourceType == null || sourceType == "Compounder")
            {
                var compounderExpired = await DetectNewExpiredCompounderMedicinesAsync(detectedBy, userPlantId);
                newExpiredMedicines.AddRange(compounderExpired);
            }

            if (sourceType == null || sourceType == "Store")
            {
                var storeExpired = await DetectNewExpiredStoreMedicinesAsync(detectedBy, userPlantId);
                newExpiredMedicines.AddRange(storeExpired);
            }

            return newExpiredMedicines;
        }

        public async Task<List<ExpiredMedicine>> DetectNewExpiredCompounderMedicinesAsync(string detectedBy, int? userPlantId = null)
        {
            var today = DateTime.Today;
            var newExpiredMedicines = new List<ExpiredMedicine>();

            // Get all expired batches from CompounderIndentBatch table WITH PLANT FILTERING
            var batchQuery = _db.Set<CompounderIndentBatch>()
                .Where(b => b.AvailableStock > 0 && b.ExpiryDate.Date < today)
                .AsQueryable();

            var expiredBatches = await batchQuery.ToListAsync();

            foreach (var batch in expiredBatches)
            {
                // Load related entities safely
                var indentItem = await _db.CompounderIndentItems
                    .Where(i => i.IndentItemId == batch.IndentItemId)
                    .FirstOrDefaultAsync();

                if (indentItem?.ReceivedQuantity > 0)
                {
                    var indent = await _db.CompounderIndents
                        .Where(ci => ci.IndentId == indentItem.IndentId)
                        .FirstOrDefaultAsync();

                    // Plant filtering
                    if (userPlantId.HasValue && indent?.plant_id != userPlantId.Value)
                        continue;

                    // Check if this specific batch is not already tracked
                    if (!await IsCompounderItemAlreadyTrackedAsync(batch.IndentItemId, batch.BatchNo, batch.ExpiryDate, userPlantId))
                    {
                        var medMaster = await _db.med_masters
                            .Where(m => m.MedItemId == indentItem.MedItemId)
                            .FirstOrDefaultAsync();

                        var expiredMedicine = new ExpiredMedicine
                        {
                            CompounderIndentItemId = batch.IndentItemId,
                            StoreIndentItemId = null,
                            SourceType = "Compounder",
                            PlantId = (short)(userPlantId ?? indent?.plant_id ?? 1),
                            MedicineName = medMaster?.MedItemName ?? "Unknown Medicine",
                            CompanyName = medMaster?.CompanyName ?? "Not Defined",
                            BatchNumber = batch.BatchNo,
                            VendorCode = batch.VendorCode,
                            ExpiryDate = batch.ExpiryDate,
                            QuantityExpired = batch.AvailableStock,
                            IndentId = indentItem.IndentId,
                            IndentNumber = indentItem.IndentId.ToString(),
                            UnitPrice = indentItem.UnitPrice,
                            TotalValue = indentItem.UnitPrice * batch.AvailableStock,
                            DetectedDate = DateTime.Now,
                            DetectedBy = detectedBy,
                            Status = "Pending Disposal",
                            TypeOfMedicine = "Select Type of Medicine"
                        };

                        newExpiredMedicines.Add(expiredMedicine);
                    }
                }
            }

            return newExpiredMedicines;
        }

        public async Task<List<ExpiredMedicine>> DetectNewExpiredStoreMedicinesAsync(string detectedBy, int? userPlantId = null)
        {
            var today = DateTime.Today;
            var newExpiredMedicines = new List<ExpiredMedicine>();

            // Get all expired batches from StoreIndentBatch table
            var batchQuery = _db.Set<StoreIndentBatch>()
                .Where(b => b.AvailableStock > 0 && b.ExpiryDate.Date < today)
                .AsQueryable();

            var expiredBatches = await batchQuery.ToListAsync();

            foreach (var batch in expiredBatches)
            {
                // Load related entities safely
                var indentItem = await _db.StoreIndentItems
                    .Where(i => i.IndentItemId == batch.IndentItemId)
                    .FirstOrDefaultAsync();

                if (indentItem?.ReceivedQuantity > 0)
                {
                    var indent = await _db.StoreIndents
                        .Where(si => si.IndentId == indentItem.IndentId)
                        .FirstOrDefaultAsync();

                    // Plant filtering
                    if (userPlantId.HasValue && indent?.PlantId != userPlantId.Value)
                        continue;

                    // Check if this specific batch is not already tracked
                    if (!await IsStoreItemAlreadyTrackedAsync(batch.IndentItemId, batch.BatchNo, batch.ExpiryDate, userPlantId))
                    {
                        var medMaster = await _db.med_masters
                            .Where(m => m.MedItemId == indentItem.MedItemId)
                            .FirstOrDefaultAsync();

                        var expiredMedicine = new ExpiredMedicine
                        {
                            CompounderIndentItemId = null,
                            StoreIndentItemId = batch.IndentItemId,
                            SourceType = "Store",
                            PlantId = (short)(userPlantId ?? indent?.PlantId ?? 1),
                            MedicineName = medMaster?.MedItemName ?? "Unknown Medicine",
                            CompanyName = medMaster?.CompanyName ?? "Not Defined",
                            BatchNumber = batch.BatchNo,
                            VendorCode = batch.VendorCode,
                            ExpiryDate = batch.ExpiryDate,
                            QuantityExpired = batch.AvailableStock,
                            IndentId = indentItem.IndentId,
                            IndentNumber = indentItem.IndentId.ToString(),
                            UnitPrice = indentItem.UnitPrice,
                            TotalValue = indentItem.UnitPrice * batch.AvailableStock,
                            DetectedDate = DateTime.Now,
                            DetectedBy = detectedBy,
                            Status = "Pending Disposal",
                            TypeOfMedicine = "Select Type of Medicine"
                        };

                        newExpiredMedicines.Add(expiredMedicine);
                    }
                }
            }

            return newExpiredMedicines;
        }

        public async Task SyncExpiredMedicinesAsync(string detectedBy, int? userPlantId = null, string? sourceType = null)
        {
            var newExpiredMedicines = await DetectNewExpiredMedicinesAsync(detectedBy, userPlantId, sourceType);

            if (newExpiredMedicines.Any())
            {
                _db.ExpiredMedicines.AddRange(newExpiredMedicines);
                await _db.SaveChangesAsync();
            }
        }

        // ======= UPDATED: Statistics and reporting WITH ROLE-BASED ACCESS =======
        //public async Task<int> GetTotalExpiredCountAsync(int? userPlantId = null, string? userRole = null)
        //{
        //    var query = _db.ExpiredMedicines.AsQueryable();

        //    // Plant filtering
        //    if (userPlantId.HasValue)
        //    {
        //        query = query.Where(e => e.PlantId == userPlantId.Value);
        //    }

        //    // Role-based filtering
        //    query = ApplyRoleBasedFilter(query, userRole);

        //    return await query.CountAsync();
        //}
        public async Task<int> GetTotalExpiredCountAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null)
        {
            var query = _db.ExpiredMedicines.AsQueryable();

            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            query = ApplyRoleBasedFilter(query, userRole);
            query = await ApplyBcmFilterAsync(query, userPlantId, userRole, currentUser);

            return await query.CountAsync();
        }
        //public async Task<int> GetPendingDisposalCountAsync(int? userPlantId = null, string? userRole = null)
        //{
        //    var query = _db.ExpiredMedicines
        //        .Where(e => e.Status == "Pending Disposal")
        //        .AsQueryable();

        //    // Plant filtering
        //    if (userPlantId.HasValue)
        //    {
        //        query = query.Where(e => e.PlantId == userPlantId.Value);
        //    }

        //    // Role-based filtering
        //    query = ApplyRoleBasedFilter(query, userRole);

        //    return await query.CountAsync();
        //}
        public async Task<int> GetPendingDisposalCountAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null)
        {
            var query = _db.ExpiredMedicines
                .Where(e => e.Status == "Pending Disposal")
                .AsQueryable();

            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            query = ApplyRoleBasedFilter(query, userRole);
            query = await ApplyBcmFilterAsync(query, userPlantId, userRole, currentUser);

            return await query.CountAsync();
        }

        //public async Task<int> GetDisposedCountAsync(int? userPlantId = null, string? userRole = null)
        //{
        //    var query = _db.ExpiredMedicines
        //        .Where(e => e.Status == "Issued to Biomedical Waste")
        //        .AsQueryable();

        //    // Plant filtering
        //    if (userPlantId.HasValue)
        //    {
        //        query = query.Where(e => e.PlantId == userPlantId.Value);
        //    }

        //    // Role-based filtering
        //    query = ApplyRoleBasedFilter(query, userRole);

        //    return await query.CountAsync();
        //}
        public async Task<int> GetDisposedCountAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null)
        {
            var query = _db.ExpiredMedicines
                .Where(e => e.Status == "Issued to Biomedical Waste")
                .AsQueryable();

            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            query = ApplyRoleBasedFilter(query, userRole);
            query = await ApplyBcmFilterAsync(query, userPlantId, userRole, currentUser);

            return await query.CountAsync();
        }

        public async Task<decimal> GetTotalExpiredValueAsync(int? userPlantId = null, string? userRole = null)
        {
            var query = _db.ExpiredMedicines
                .Where(e => e.TotalValue.HasValue)
                .AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            return await query.SumAsync(e => e.TotalValue.Value);
        }

        public async Task<IEnumerable<ExpiredMedicine>> GetExpiredMedicinesForPrintAsync(List<int> ids, int? userPlantId = null, string? userRole = null)
        {
            var query = _db.ExpiredMedicines
                .Where(e => ids.Contains(e.ExpiredMedicineId))
                .AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            var results = await query
                .OrderBy(e => e.MedicineName)
                .ToListAsync();

            // Load navigation properties for each item
            foreach (var item in results)
            {
                await LoadNavigationPropertiesAsync(item);
            }

            return results;
        }

        // ======= UPDATED: Inline editing method WITH ROLE-BASED ACCESS =======
        public async Task UpdateMedicineTypeAsync(int expiredMedicineId, string typeOfMedicine, int? userPlantId = null, string? userRole = null)
        {
            var query = _db.ExpiredMedicines.AsQueryable();

            // Plant filtering
            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            var expiredMedicine = await query.FirstOrDefaultAsync(e => e.ExpiredMedicineId == expiredMedicineId);
            if (expiredMedicine != null)
            {
                expiredMedicine.TypeOfMedicine = typeOfMedicine;
                await _db.SaveChangesAsync();
            }
        }

        // ======= HELPER METHODS FOR PLANT-BASED OPERATIONS AND ROLE-BASED ACCESS =======
        public async Task<int?> GetUserPlantIdAsync(string userName)
        {
            var user = await _db.SysUsers
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.plant_id;
        }

        public async Task<string?> GetUserRoleAsync(string userName)
        {
            var user = await _db.SysUsers
                .Include(u => u.SysRole)
                .FirstOrDefaultAsync(u => (u.adid == userName || u.email == userName || u.full_name == userName) && u.is_active);

            return user?.SysRole?.role_name;
        }

        public async Task<bool> IsUserAuthorizedForExpiredMedicineAsync(int expiredMedicineId, int userPlantId, string? userRole = null)
        {
            var query = _db.ExpiredMedicines
                .Where(e => e.ExpiredMedicineId == expiredMedicineId &&
                           e.PlantId.HasValue && e.PlantId.Value == userPlantId)
                .AsQueryable();

            // Role-based filtering
            query = ApplyRoleBasedFilter(query, userRole);

            return await query.AnyAsync();
        }

        // ======= FIXED: Source type validation methods =======
        public async Task<bool> CanUserAccessSourceTypeAsync(string sourceType, string? userRole)
        {
            if (string.IsNullOrEmpty(userRole))
                return true;

            var roleLower = userRole.ToLower();

            if (roleLower.Contains("store"))
                return sourceType == "Store";
            else if (roleLower.Contains("compounder"))
                return sourceType == "Compounder";
            else if (roleLower.Contains("doctor"))
                return true; // Doctors can access both

            return true; // Default: allow access if role doesn't match known patterns
        }

        public async Task<List<string>> GetAccessibleSourceTypesAsync(string? userRole)
        {
            if (string.IsNullOrEmpty(userRole))
                return new List<string> { "Store", "Compounder" };

            var roleLower = userRole.ToLower();

            if (roleLower.Contains("store"))
                return new List<string> { "Store" };
            else if (roleLower.Contains("compounder"))
                return new List<string> { "Compounder" };
            else if (roleLower.Contains("doctor"))
                return new List<string> { "Store", "Compounder" };

            return new List<string> { "Store", "Compounder" }; // Default: both
        }

        // ======= NEW: Role-based statistics =======
        //public async Task<Dictionary<string, int>> GetStatisticsBySourceTypeAsync(int? userPlantId = null, string? userRole = null)
        //{
        //    var query = _db.ExpiredMedicines.AsQueryable();

        //    // Plant filtering
        //    if (userPlantId.HasValue)
        //    {
        //        query = query.Where(e => e.PlantId == userPlantId.Value);
        //    }

        //    // Role-based filtering
        //    query = ApplyRoleBasedFilter(query, userRole);

        //    var stats = await query
        //        .GroupBy(e => e.SourceType)
        //        .Select(g => new { SourceType = g.Key, Count = g.Count() })
        //        .ToDictionaryAsync(x => x.SourceType, x => x.Count);

        //    // Ensure both source types are represented based on user role
        //    var accessibleSourceTypes = await GetAccessibleSourceTypesAsync(userRole);

        //    foreach (var sourceType in accessibleSourceTypes)
        //    {
        //        if (!stats.ContainsKey(sourceType))
        //            stats[sourceType] = 0;
        //    }

        //    return stats;
        //}
        public async Task<Dictionary<string, int>> GetStatisticsBySourceTypeAsync(int? userPlantId = null, string? userRole = null, string? currentUser = null)
        {
            var query = _db.ExpiredMedicines.AsQueryable();

            if (userPlantId.HasValue)
            {
                query = query.Where(e => e.PlantId == userPlantId.Value);
            }

            query = ApplyRoleBasedFilter(query, userRole);
            query = await ApplyBcmFilterAsync(query, userPlantId, userRole, currentUser);

            var stats = await query
                .GroupBy(e => e.SourceType)
                .Select(g => new { SourceType = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SourceType, x => x.Count);

            var accessibleSourceTypes = await GetAccessibleSourceTypesAsync(userRole);

            foreach (var sourceType in accessibleSourceTypes)
            {
                if (!stats.ContainsKey(sourceType))
                    stats[sourceType] = 0;
            }

            return stats;
        }
    }
}