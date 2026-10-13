using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Data.Enums;

namespace PropertyRentalPortal.Web.Data.Seeding;

public static class DatabaseSeeder
{
    public const string PropertyManagerRole = "PropertyManager";
    public const string ApplicantRole = "Applicant";

    public const string DefaultManagerEmail = "manager@rental.com";
    public const string DefaultApplicantEmail = "applicant@rental.com";
    public const string DefaultPassword = "Password123!";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Ensure Roles
        if (!await roleManager.RoleExistsAsync(PropertyManagerRole))
        {
            await roleManager.CreateAsync(new IdentityRole(PropertyManagerRole));
        }
        if (!await roleManager.RoleExistsAsync(ApplicantRole))
        {
            await roleManager.CreateAsync(new IdentityRole(ApplicantRole));
        }

        // 2. Ensure Default Users
        var managerUser = await userManager.FindByEmailAsync(DefaultManagerEmail);
        if (managerUser == null)
        {
            managerUser = new ApplicationUser
            {
                UserName = DefaultManagerEmail,
                Email = DefaultManagerEmail,
                EmailConfirmed = true,
                FullName = "J.T. Pierce (Senior Property Manager)"
            };
            var createRes = await userManager.CreateAsync(managerUser, DefaultPassword);
            if (createRes.Succeeded)
            {
                await userManager.AddToRoleAsync(managerUser, PropertyManagerRole);
            }
        }

        var applicantUser = await userManager.FindByEmailAsync(DefaultApplicantEmail);
        if (applicantUser == null)
        {
            applicantUser = new ApplicationUser
            {
                UserName = DefaultApplicantEmail,
                Email = DefaultApplicantEmail,
                EmailConfirmed = true,
                FullName = "Farquar Gazocket (Applicant)"
            };
            var createRes = await userManager.CreateAsync(applicantUser, DefaultPassword);
            if (createRes.Succeeded)
            {
                await userManager.AddToRoleAsync(applicantUser, ApplicantRole);
            }
        }

        // Additional seeded applicants using Bogus for realistic tenant data
        var extraApplicants = new List<ApplicationUser>();
        for (int i = 1; i <= 4; i++)
        {
            var email = $"applicant{i}@rental.com";
            var existing = await userManager.FindByEmailAsync(email);
            if (existing == null)
            {
                var faker = new Faker("en");
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = faker.Name.FullName()
                };
                var res = await userManager.CreateAsync(user, DefaultPassword);
                if (res.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, ApplicantRole);
                    extraApplicants.Add(user);
                }
            }
            else
            {
                extraApplicants.Add(existing);
            }
        }

        // 3. Ensure Lookups (Unit Types)
        // Includes active types and inactive types (to demonstrate inactive display on existing units)
        var unitTypeNames = new[]
        {
            new { Name = "Studio", IsActive = true },
            new { Name = "1 Bedroom", IsActive = true },
            new { Name = "2 Bedroom", IsActive = true },
            new { Name = "3 Bedroom", IsActive = true },
            new { Name = "Townhome", IsActive = true },
            new { Name = "Penthouse", IsActive = true },
            new { Name = "Legacy Loft", IsActive = false },
            new { Name = "Micro Suite", IsActive = false }
        };

        foreach (var ut in unitTypeNames)
        {
            if (!await context.UnitTypes.AnyAsync(x => x.Name == ut.Name))
            {
                context.UnitTypes.Add(new UnitType { Name = ut.Name, IsActive = ut.IsActive });
            }
        }
        await context.SaveChangesAsync();

        var unitTypes = await context.UnitTypes.ToListAsync();
        var studio = unitTypes.First(x => x.Name == "Studio");
        var oneBed = unitTypes.First(x => x.Name == "1 Bedroom");
        var twoBed = unitTypes.First(x => x.Name == "2 Bedroom");
        var threeBed = unitTypes.First(x => x.Name == "3 Bedroom");
        var legacyLoft = unitTypes.First(x => x.Name == "Legacy Loft");

        // 4. Ensure Properties and Units (All Virginia Locations)
        if (!await context.Properties.AnyAsync())
        {
            var prop1 = new Property
            {
                Name = "Grandview Heights",
                Address = "8200 Old Dominion Dr",
                City = "McLean",
                State = "VA",
                ZipCode = "22102",
                Description = "Luxury Northern Virginia apartment community featuring panoramic views, modern fitness center, and rooftop terrace."
            };

            var prop2 = new Property
            {
                Name = "Potomac Landing Residences",
                Address = "3000 Potomac Ave",
                City = "Alexandria",
                State = "VA",
                ZipCode = "22305",
                Description = "Waterfront apartments along the Potomac with direct trail access, contemporary finishes, and in-unit laundry."
            };

            var prop3 = new Property
            {
                Name = "Oakwood Park Suites",
                Address = "1550 Monument Ave",
                City = "Richmond",
                State = "VA",
                ZipCode = "23220",
                Description = "Historic Richmond living with tree-lined walkways, sparkling resort pool, and pet-friendly amenities."
            };

            context.Properties.AddRange(prop1, prop2, prop3);
            await context.SaveChangesAsync();

            // Seed Units for Properties
            var units = new List<Unit>
            {
                // Grandview Heights units (McLean, VA)
                new Unit { PropertyId = prop1.Id, UnitNumber = "101", Bedrooms = 1, Bathrooms = 1.0m, MonthlyRent = 1850.00m, UnitTypeId = oneBed.Id },
                new Unit { PropertyId = prop1.Id, UnitNumber = "102", Bedrooms = 2, Bathrooms = 2.0m, MonthlyRent = 2450.00m, UnitTypeId = twoBed.Id },
                new Unit { PropertyId = prop1.Id, UnitNumber = "201", Bedrooms = 0, Bathrooms = 1.0m, MonthlyRent = 1500.00m, UnitTypeId = studio.Id },
                new Unit { PropertyId = prop1.Id, UnitNumber = "202", Bedrooms = 2, Bathrooms = 1.5m, MonthlyRent = 2300.00m, UnitTypeId = twoBed.Id },
                new Unit { PropertyId = prop1.Id, UnitNumber = "301", Bedrooms = 3, Bathrooms = 2.0m, MonthlyRent = 2950.00m, UnitTypeId = threeBed.Id },
                // Unit with inactive unit type (Legacy Loft) to satisfy lookup constraint test
                new Unit { PropertyId = prop1.Id, UnitNumber = "PH-A", Bedrooms = 2, Bathrooms = 2.5m, MonthlyRent = 3400.00m, UnitTypeId = legacyLoft.Id },

                // Potomac Landing units (Alexandria, VA)
                new Unit { PropertyId = prop2.Id, UnitNumber = "A-1", Bedrooms = 1, Bathrooms = 1.0m, MonthlyRent = 1950.00m, UnitTypeId = oneBed.Id },
                new Unit { PropertyId = prop2.Id, UnitNumber = "A-2", Bedrooms = 2, Bathrooms = 2.0m, MonthlyRent = 2600.00m, UnitTypeId = twoBed.Id },
                new Unit { PropertyId = prop2.Id, UnitNumber = "B-1", Bedrooms = 1, Bathrooms = 1.0m, MonthlyRent = 2050.00m, UnitTypeId = oneBed.Id },
                new Unit { PropertyId = prop2.Id, UnitNumber = "B-2", Bedrooms = 3, Bathrooms = 2.5m, MonthlyRent = 3200.00m, UnitTypeId = threeBed.Id },

                // Oakwood Park Suites units (Richmond, VA)
                new Unit { PropertyId = prop3.Id, UnitNumber = "10", Bedrooms = 1, Bathrooms = 1.0m, MonthlyRent = 1450.00m, UnitTypeId = oneBed.Id },
                new Unit { PropertyId = prop3.Id, UnitNumber = "12", Bedrooms = 2, Bathrooms = 1.0m, MonthlyRent = 1850.00m, UnitTypeId = twoBed.Id },
                new Unit { PropertyId = prop3.Id, UnitNumber = "14", Bedrooms = 2, Bathrooms = 2.0m, MonthlyRent = 1950.00m, UnitTypeId = twoBed.Id }
            };

            context.Units.AddRange(units);
            await context.SaveChangesAsync();
        }

        // 5. Ensure Applications in EVERY Status (All Virginia Addresses)
        // Requirement: "The database should be seeded idempotently with lookups, property managers, applicants, properties, units, and applications in every status."
        // Statuses: Draft, Submitted, UnderReview, Returned, Approved, Denied, Withdrawn
        if (!await context.RentalApplications.AnyAsync())
        {
            var unitsList = await context.Units.Include(u => u.Property).ToListAsync();
            var manager = (await userManager.FindByEmailAsync(DefaultManagerEmail))!;
            var applicant = (await userManager.FindByEmailAsync(DefaultApplicantEmail))!;

            var applicant1 = extraApplicants.ElementAtOrDefault(0) ?? applicant;
            var applicant2 = extraApplicants.ElementAtOrDefault(1) ?? applicant;
            var applicant3 = extraApplicants.ElementAtOrDefault(2) ?? applicant;
            var applicant4 = extraApplicants.ElementAtOrDefault(3) ?? applicant;

            // Virginia cities catalog for prior residences
            var vaCities = new[]
            {
                new { City = "Arlington", Zip = "22201", AreaCode = "703" },
                new { City = "Alexandria", Zip = "22314", AreaCode = "703" },
                new { City = "Richmond", Zip = "23220", AreaCode = "804" },
                new { City = "Virginia Beach", Zip = "23451", AreaCode = "757" },
                new { City = "Charlottesville", Zip = "22902", AreaCode = "434" },
                new { City = "Fairfax", Zip = "22030", AreaCode = "703" },
                new { City = "Norfolk", Zip = "23510", AreaCode = "757" },
                new { City = "Reston", Zip = "20190", AreaCode = "571" },
                new { City = "Roanoke", Zip = "24011", AreaCode = "540" },
                new { City = "Williamsburg", Zip = "23185", AreaCode = "757" }
            };

            // Seed helper: generate prior residences using Bogus with Virginia addresses
            List<ResidenceHistory> GenerateResidences(int count)
            {
                var f = new Faker("en");
                var list = new List<ResidenceHistory>();
                var currentYear = DateTime.UtcNow.Year;
                for (int i = 0; i < count; i++)
                {
                    var vaLoc = f.PickRandom(vaCities);
                    var moveIn = new DateTime(currentYear - (i + 2), f.Random.Int(1, 12), f.Random.Int(1, 28));
                    var moveOut = moveIn.AddYears(1).AddMonths(f.Random.Int(1, 6));
                    list.Add(new ResidenceHistory
                    {
                        Address = f.Address.StreetAddress(),
                        City = vaLoc.City,
                        State = "VA",
                        ZipCode = vaLoc.Zip,
                        LandlordName = f.Name.FullName(),
                        LandlordPhone = $"{vaLoc.AreaCode}-555-{f.Random.Number(1000, 9999)}",
                        MoveInDate = moveIn,
                        MoveOutDate = moveOut,
                        CreatedAt = DateTime.UtcNow.AddMonths(-i)
                    });
                }
                return list;
            }

            // 1. DRAFT Application (Applicant: Default Applicant, Unit: 101 McLean, VA)
            var appDraft = new RentalApplication
            {
                UnitId = unitsList[0].Id,
                ApplicantUserId = applicant.Id,
                Status = ApplicationStatus.Draft,
                CurrentSection = 1,
                ApplicantInfoSaved = true,
                ResidenceHistorySaved = false,
                ApplicantFullName = applicant.FullName,
                ApplicantEmail = applicant.Email!,
                ApplicantPhone = "703-555-0199",
                CurrentAddress = "1101 Wilson Blvd, Apt 502",
                CurrentCity = "Arlington",
                CurrentState = "VA",
                CurrentZipCode = "22209",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Residences = GenerateResidences(1)
            };
            appDraft.StatusHistories.Add(new ApplicationStatusHistory
            {
                OldStatus = null,
                NewStatus = ApplicationStatus.Draft,
                ChangedByUserId = applicant.Id,
                ChangedAt = DateTime.UtcNow.AddDays(-2),
                Comment = "Started new draft application."
            });

            // 2. SUBMITTED Application (Applicant: Default Applicant, Unit: 102 McLean, VA)
            var appSubmitted = new RentalApplication
            {
                UnitId = unitsList[1].Id,
                ApplicantUserId = applicant.Id,
                Status = ApplicationStatus.Submitted,
                CurrentSection = 3,
                ApplicantInfoSaved = true,
                ResidenceHistorySaved = true,
                ApplicantFullName = applicant.FullName,
                ApplicantEmail = applicant.Email!,
                ApplicantPhone = "703-555-0199",
                CurrentAddress = "1101 Wilson Blvd, Apt 502",
                CurrentCity = "Arlington",
                CurrentState = "VA",
                CurrentZipCode = "22209",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                SubmittedAt = DateTime.UtcNow.AddDays(-4),
                Residences = GenerateResidences(2)
            };
            appSubmitted.StatusHistories.Add(new ApplicationStatusHistory
            {
                OldStatus = ApplicationStatus.Draft,
                NewStatus = ApplicationStatus.Submitted,
                ChangedByUserId = applicant.Id,
                ChangedAt = DateTime.UtcNow.AddDays(-4),
                Comment = "Application completed and submitted for review."
            });

            // 3. UNDER REVIEW Application (Unit: 201 McLean, VA - Claimed by manager in Review Queue)
            var appUnderReview = new RentalApplication
            {
                UnitId = unitsList[2].Id,
                ApplicantUserId = applicant1.Id,
                Status = ApplicationStatus.UnderReview,
                CurrentSection = 3,
                ApplicantInfoSaved = true,
                ResidenceHistorySaved = true,
                ApplicantFullName = applicant1.FullName,
                ApplicantEmail = applicant1.Email!,
                ApplicantPhone = "434-555-0144",
                CurrentAddress = "400 W Main St",
                CurrentCity = "Charlottesville",
                CurrentState = "VA",
                CurrentZipCode = "22903",
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                SubmittedAt = DateTime.UtcNow.AddDays(-5),
                ClaimedByUserId = manager.Id,
                ClaimedAt = DateTime.UtcNow.AddDays(-1),
                Residences = GenerateResidences(2)
            };
            appUnderReview.StatusHistories.Add(new ApplicationStatusHistory
            {
                OldStatus = ApplicationStatus.Submitted,
                NewStatus = ApplicationStatus.UnderReview,
                ChangedByUserId = manager.Id,
                ChangedAt = DateTime.UtcNow.AddDays(-1),
                Comment = "Claimed by property manager for background and credit verification."
            });
            appUnderReview.Notes.Add(new PropertyManagerNote
            {
                AuthorUserId = manager.Id,
                Content = "Applicant credit report checked (740 score). Awaiting landlord reference call.",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });

            // 4. RETURNED Application (Unit: 202 McLean, VA - Returned with comment)
            var appReturned = new RentalApplication
            {
                UnitId = unitsList[3].Id,
                ApplicantUserId = applicant2.Id,
                Status = ApplicationStatus.Returned,
                CurrentSection = 2,
                ApplicantInfoSaved = true,
                ResidenceHistorySaved = true,
                ApplicantFullName = applicant2.FullName,
                ApplicantEmail = applicant2.Email!,
                ApplicantPhone = "571-555-0177",
                CurrentAddress = "2200 Crystal Dr, Apt 814",
                CurrentCity = "Arlington",
                CurrentState = "VA",
                CurrentZipCode = "22202",
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                SubmittedAt = DateTime.UtcNow.AddDays(-6),
                Residences = GenerateResidences(1)
            };
            appReturned.StatusHistories.Add(new ApplicationStatusHistory
            {
                OldStatus = ApplicationStatus.Submitted,
                NewStatus = ApplicationStatus.Returned,
                ChangedByUserId = manager.Id,
                ChangedAt = DateTime.UtcNow.AddDays(-3),
                Comment = "Please provide landlord contact information and move-out date for your residence in 2024."
            });

            // 5. APPROVED Application (Unit: 301 McLean, VA - Issues 12-month lease!)
            var approvedUnit = unitsList[4]; // 301
            var today = DateTime.UtcNow.Date;
            var leaseStartDate = today.AddDays(-15);
            var leaseEndDate = leaseStartDate.AddYears(1).AddDays(-1);

            var appApproved = new RentalApplication
            {
                UnitId = approvedUnit.Id,
                ApplicantUserId = applicant3.Id,
                Status = ApplicationStatus.Approved,
                CurrentSection = 3,
                ApplicantInfoSaved = true,
                ResidenceHistorySaved = true,
                ApplicantFullName = applicant3.FullName,
                ApplicantEmail = applicant3.Email!,
                ApplicantPhone = "804-555-0811",
                CurrentAddress = "501 E Franklin St",
                CurrentCity = "Richmond",
                CurrentState = "VA",
                CurrentZipCode = "23219",
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                SubmittedAt = DateTime.UtcNow.AddDays(-18),
                CompletedAt = leaseStartDate,
                Residences = GenerateResidences(2)
            };
            appApproved.StatusHistories.Add(new ApplicationStatusHistory
            {
                OldStatus = ApplicationStatus.Submitted,
                NewStatus = ApplicationStatus.Approved,
                ChangedByUserId = manager.Id,
                ChangedAt = leaseStartDate,
                Comment = "All background and financial verifications cleared. Approved for 12-month lease."
            });

            // Create issued 12-month lease for the approved unit
            var lease = new Lease
            {
                UnitId = approvedUnit.Id,
                TenantUserId = applicant3.Id,
                RentalApplication = appApproved,
                StartDate = leaseStartDate,
                EndDate = leaseEndDate,
                MonthlyRent = approvedUnit.MonthlyRent,
                CreatedAt = leaseStartDate
            };
            appApproved.Lease = lease;

            // 6. DENIED Application (Unit: A-1 Alexandria, VA - Terminal status with comment)
            var appDenied = new RentalApplication
            {
                UnitId = unitsList[6].Id,
                ApplicantUserId = applicant4.Id,
                Status = ApplicationStatus.Denied,
                CurrentSection = 3,
                ApplicantInfoSaved = true,
                ResidenceHistorySaved = true,
                ApplicantFullName = applicant4.FullName,
                ApplicantEmail = applicant4.Email!,
                ApplicantPhone = "757-555-0122",
                CurrentAddress = "1950 Chain Bridge Rd",
                CurrentCity = "Tysons",
                CurrentState = "VA",
                CurrentZipCode = "22182",
                CreatedAt = DateTime.UtcNow.AddDays(-12),
                SubmittedAt = DateTime.UtcNow.AddDays(-10),
                CompletedAt = DateTime.UtcNow.AddDays(-8),
                Residences = GenerateResidences(1)
            };
            appDenied.StatusHistories.Add(new ApplicationStatusHistory
            {
                OldStatus = ApplicationStatus.Submitted,
                NewStatus = ApplicationStatus.Denied,
                ChangedByUserId = manager.Id,
                ChangedAt = DateTime.UtcNow.AddDays(-8),
                Comment = "Application denied due to incomplete income verification documentation."
            });

            // 7. WITHDRAWN Application (Unit: A-2 Alexandria, VA - Terminal status withdrawn by applicant)
            var appWithdrawn = new RentalApplication
            {
                UnitId = unitsList[7].Id,
                ApplicantUserId = applicant.Id,
                Status = ApplicationStatus.Withdrawn,
                CurrentSection = 2,
                ApplicantInfoSaved = true,
                ResidenceHistorySaved = false,
                ApplicantFullName = applicant.FullName,
                ApplicantEmail = applicant.Email!,
                ApplicantPhone = "703-555-0199",
                CurrentAddress = "1101 Wilson Blvd, Apt 502",
                CurrentCity = "Arlington",
                CurrentState = "VA",
                CurrentZipCode = "22209",
                CreatedAt = DateTime.UtcNow.AddDays(-14),
                CompletedAt = DateTime.UtcNow.AddDays(-13),
                Residences = GenerateResidences(1)
            };
            appWithdrawn.StatusHistories.Add(new ApplicationStatusHistory
            {
                OldStatus = ApplicationStatus.Draft,
                NewStatus = ApplicationStatus.Withdrawn,
                ChangedByUserId = applicant.Id,
                ChangedAt = DateTime.UtcNow.AddDays(-13),
                Comment = "Withdrawn by applicant - relocated to another city."
            });

            context.RentalApplications.AddRange(
                appDraft,
                appSubmitted,
                appUnderReview,
                appReturned,
                appApproved,
                appDenied,
                appWithdrawn
            );

            await context.SaveChangesAsync();
        }
    }
}
