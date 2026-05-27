using GymSaaS.Models;
using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GymSaaS.Controllers
{
    [Authorize(Policy = "ManagerAndAbove")]
    public class WorkoutTemplatesController : Controller
    {
        private readonly GymDbContext _db;

        public WorkoutTemplatesController(GymDbContext db)
        {
            _db = db;
        }

        private Guid TenantId =>
            Guid.Parse(User.FindFirstValue("TenantId")!);

        private Guid UserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET /WorkoutTemplates
        public async Task<IActionResult> Index(string? search, byte? difficulty, bool? activeOnly)
        {
            var query = _db.WorkoutTemplates
                .Where(t => t.TenantId == TenantId && !t.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t => t.TemplateName.Contains(search));

            if (difficulty.HasValue)
                query = query.Where(t => t.Difficulty == difficulty.Value);

            if (activeOnly == true)
                query = query.Where(t => t.IsActive);

            var raw = await query
                .OrderByDescending(t => t.CreatedAtUtc)
                .Select(t => new
                {
                    t.WorkoutTemplateId,
                    t.TemplateName,
                    t.Description,
                    t.Difficulty,
                    t.EstimatedMinutes,
                    t.IsActive,
                    t.CreatedAtUtc,
                })
                .ToListAsync();

            var templateIds = raw.Select(t => t.WorkoutTemplateId).ToList();
            var counts = await _db.WorkoutTemplateExercises
                .Where(e => templateIds.Contains(e.WorkoutTemplateId))
                .GroupBy(e => e.WorkoutTemplateId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            var templates = raw.Select(t => new WorkoutTemplateListItem
            {
                WorkoutTemplateId = t.WorkoutTemplateId,
                TemplateName = t.TemplateName,
                Description = t.Description,
                Difficulty = t.Difficulty,
                EstimatedMinutes = t.EstimatedMinutes,
                IsActive = t.IsActive,
                CreatedAtUtc = t.CreatedAtUtc,
                ExerciseCount = counts.TryGetValue(t.WorkoutTemplateId, out var c) ? c : 0,
            }).ToList();

            ViewData["Title"] = "Workout Templates";
            ViewData["Search"] = search;
            ViewData["Difficulty"] = difficulty;
            ViewData["ActiveOnly"] = activeOnly;
            ViewData["TotalCount"] = templates.Count;
            ViewData["ActiveCount"] = templates.Count(t => t.IsActive);

            return View(templates);
        }

        // GET /WorkoutTemplates/Create
        public async Task<IActionResult> Create()
        {
            var vm = new WorkoutTemplateFormViewModel
            {
                AvailableExercises = await GetExercisePickerAsync(),
            };
            ViewData["Title"] = "Workout Templates";
            ViewData["Subtitle"] = "New Template";
            return View("CreateEdit", vm);
        }

        // POST /WorkoutTemplates/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WorkoutTemplateFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableExercises = await GetExercisePickerAsync();
                ViewData["Title"] = "Workout Templates";
                ViewData["Subtitle"] = "New Template";
                return View("CreateEdit", model);
            }

            var templateId = Guid.NewGuid();
            var template = new WorkoutTemplate
            {
                WorkoutTemplateId = templateId,
                TenantId = TenantId,
                TemplateName = model.TemplateName.Trim(),
                Description = model.Description?.Trim(),
                Difficulty = model.Difficulty,
                EstimatedMinutes = model.EstimatedMinutes,
                IsActive = model.IsActive,
                IsDeleted = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = UserId,
            };

            _db.WorkoutTemplates.Add(template);

            for (int i = 0; i < model.Exercises.Count; i++)
            {
                var e = model.Exercises[i];
                _db.WorkoutTemplateExercises.Add(new WorkoutTemplateExercise
                {
                    WorkoutTemplateExerciseId = Guid.NewGuid(),
                    WorkoutTemplateId = templateId,
                    ExerciseId = e.ExerciseId,
                    SortOrder = i,
                    Sets = e.Sets,
                    Reps = e.Reps,
                    DurationSeconds = e.DurationSeconds,
                    RestSeconds = e.RestSeconds,
                    Notes = e.Notes?.Trim(),
                });
            }

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Template \"{template.TemplateName}\" created successfully.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // GET /WorkoutTemplates/Edit/id
        public async Task<IActionResult> Edit(Guid id)
        {
            var t = await _db.WorkoutTemplates
                .Include(x => x.WorkoutTemplateExercises)
                    .ThenInclude(e => e.Exercise)
                        .ThenInclude(e => e.Category)
                .FirstOrDefaultAsync(x => x.WorkoutTemplateId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (t == null) return NotFound();

            var vm = new WorkoutTemplateFormViewModel
            {
                WorkoutTemplateId = t.WorkoutTemplateId,
                TemplateName = t.TemplateName,
                Description = t.Description,
                Difficulty = t.Difficulty,
                EstimatedMinutes = t.EstimatedMinutes,
                IsActive = t.IsActive,
                Exercises = t.WorkoutTemplateExercises
                    .OrderBy(e => e.SortOrder)
                    .Select(e => new WorkoutTemplateExerciseFormItem
                    {
                        ExerciseId = e.ExerciseId,
                        ExerciseName = e.Exercise.ExerciseName,
                        CategoryName = e.Exercise.Category.CategoryName,
                        SortOrder = e.SortOrder,
                        Sets = e.Sets,
                        Reps = e.Reps,
                        DurationSeconds = e.DurationSeconds,
                        RestSeconds = e.RestSeconds,
                        Notes = e.Notes,
                    })
                    .ToList(),
                AvailableExercises = await GetExercisePickerAsync(),
            };

            ViewData["Title"] = "Workout Templates";
            ViewData["Subtitle"] = "Edit Template";
            return View("CreateEdit", vm);
        }

        // POST /WorkoutTemplates/Edit/id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, WorkoutTemplateFormViewModel model)
        {
            var t = await _db.WorkoutTemplates
                .Include(x => x.WorkoutTemplateExercises)
                .FirstOrDefaultAsync(x => x.WorkoutTemplateId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (t == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.AvailableExercises = await GetExercisePickerAsync();
                ViewData["Title"] = "Workout Templates";
                ViewData["Subtitle"] = "Edit Template";
                return View("CreateEdit", model);
            }

            t.TemplateName = model.TemplateName.Trim();
            t.Description = model.Description?.Trim();
            t.Difficulty = model.Difficulty;
            t.EstimatedMinutes = model.EstimatedMinutes;
            t.IsActive = model.IsActive;
            t.UpdatedAtUtc = DateTime.UtcNow;
            t.UpdatedByUserId = UserId;

            _db.WorkoutTemplateExercises.RemoveRange(t.WorkoutTemplateExercises);

            for (int i = 0; i < model.Exercises.Count; i++)
            {
                var e = model.Exercises[i];
                _db.WorkoutTemplateExercises.Add(new WorkoutTemplateExercise
                {
                    WorkoutTemplateExerciseId = Guid.NewGuid(),
                    WorkoutTemplateId = id,
                    ExerciseId = e.ExerciseId,
                    SortOrder = i,
                    Sets = e.Sets,
                    Reps = e.Reps,
                    DurationSeconds = e.DurationSeconds,
                    RestSeconds = e.RestSeconds,
                    Notes = e.Notes?.Trim(),
                });
            }

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"\"{t.TemplateName}\" updated.";
            TempData["ToastType"] = "success";
            return RedirectToAction(nameof(Index));
        }

        // POST /WorkoutTemplates/Delete/id (soft delete)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var t = await _db.WorkoutTemplates
                .FirstOrDefaultAsync(x => x.WorkoutTemplateId == id && x.TenantId == TenantId && !x.IsDeleted);

            if (t == null) return NotFound();

            t.IsDeleted = true;
            t.IsActive = false;
            t.UpdatedAtUtc = DateTime.UtcNow;
            t.UpdatedByUserId = UserId;

            await _db.SaveChangesAsync();

            TempData["Toast"] = $"Template \"{t.TemplateName}\" removed.";
            TempData["ToastType"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        // ── Helpers ──────────────────────────────────

        private async Task<List<ExercisePickerItem>> GetExercisePickerAsync() =>
            await _db.Exercises
                .Where(e => e.TenantId == TenantId && !e.IsDeleted && e.IsActive)
                .OrderBy(e => e.ExerciseName)
                .Select(e => new ExercisePickerItem
                {
                    ExerciseId = e.ExerciseId,
                    ExerciseName = e.ExerciseName,
                    CategoryName = e.Category.CategoryName,
                })
                .ToListAsync();
    }
}
