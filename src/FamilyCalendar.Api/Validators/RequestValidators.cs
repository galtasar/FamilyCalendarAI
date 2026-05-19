using FamilyCalendar.Core.Interfaces;
using FluentValidation;

namespace FamilyCalendar.Api.Validators;

public class ProcessEmailRequestValidator : AbstractValidator<ProcessEmailRequest>
{
    public ProcessEmailRequestValidator()
    {
        RuleFor(x => x.MessageId)
            .NotEmpty().WithMessage("MessageId är obligatoriskt");

        RuleFor(x => x.Sender)
            .NotEmpty().WithMessage("Sender är obligatoriskt")
            .EmailAddress().WithMessage("Sender måste vara en giltig e-postadress");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject är obligatoriskt")
            .MaximumLength(500).WithMessage("Subject får inte vara längre än 500 tecken");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body är obligatoriskt");

        RuleFor(x => x.ReceivedAt)
            .NotEqual(default(DateTimeOffset)).WithMessage("ReceivedAt måste anges");
    }
}

public class UpdateEventRequestValidator : AbstractValidator<UpdateEventRequest>
{
    private readonly IFamilyMemberRepository _familyMemberRepo;

    public UpdateEventRequestValidator(IFamilyMemberRepository familyMemberRepo)
    {
        _familyMemberRepo = familyMemberRepo;

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title får inte vara tom om den anges")
            .MaximumLength(200).WithMessage("Title får inte vara längre än 200 tecken")
            .When(x => x.Title is not null);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description får inte vara längre än 2000 tecken")
            .When(x => x.Description is not null);

        RuleFor(x => x.Location)
            .MaximumLength(300).WithMessage("Location får inte vara längre än 300 tecken")
            .When(x => x.Location is not null);

        RuleFor(x => x.FamilyMemberName)
            .MustAsync(async (name, ct) =>
            {
                if (string.IsNullOrWhiteSpace(name)) return true; // other rules handle empty
                var familyMembers = await _familyMemberRepo.GetAllAsync(ct);
                return familyMembers.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            })
            .WithMessage("Ogiltigt familjemedlemsnamn.")
            .When(x => x.FamilyMemberName is not null);

        RuleFor(x => x)
            .Must(x => x.EndTime == null || x.StartTime == null || x.EndTime > x.StartTime)
            .WithMessage("EndTime måste vara efter StartTime")
            .When(x => x.StartTime.HasValue && x.EndTime.HasValue);
    }
}
