using MudBlazor;

namespace Pantrix.Themes;

/// <summary>
/// A look the app can wear. The colours, fonts and corners live in <see cref="Theme"/>; anything that can't be said
/// there is CSS in app.css under <c>html[data-theme="{Id}"]</c>.
/// </summary>
/// <param name="Id">Saved in the browser and set as <c>data-theme</c> on the page, so it must never change.</param>
/// <param name="AlwaysDark">The theme has one set of colours, so the light/dark switch doesn't apply to it.</param>
public sealed record AppTheme(string Id, string Name, MudTheme Theme, bool AlwaysDark = false);

public static class AppThemes
{
    public static readonly AppTheme Classic = new("classic", "Classic", new MudTheme());

    public static readonly AppTheme Arcade = new("arcade", "Arcade", BuildArcade(), AlwaysDark: true);

    /// <summary>Every theme on offer, in the order the picker lists them. The first is the default.</summary>
    public static readonly IReadOnlyList<AppTheme> All = [Classic, Arcade];

    /// <summary>The theme saved under <paramref name="id"/>, or the default when there is none (or it no longer exists).</summary>
    public static AppTheme Find(string? id) => All.FirstOrDefault(theme => theme.Id == id) ?? All[0];

    private static MudTheme BuildArcade()
    {
        // The chunky arcade font is far too wide for running text, so it is kept to headings.
        string[] heading = ["Press Start 2P", "Pixelify Sans", "monospace"];
        string[] body = ["Pixelify Sans", "Courier New", "monospace"];

        return new MudTheme
        {
            PaletteDark = new PaletteDark
            {
                Black = "#000000",
                Primary = "#ffd23f",
                PrimaryContrastText = "#000000",
                Secondary = "#ff4fd8",
                SecondaryContrastText = "#000000",
                Tertiary = "#29e6ff",
                TertiaryContrastText = "#000000",
                Info = "#4d9bff",
                InfoContrastText = "#000000",
                Success = "#3ddc52",
                SuccessContrastText = "#000000",
                Warning = "#ff9a3c",
                WarningContrastText = "#000000",
                Error = "#ff4d4d",
                ErrorContrastText = "#000000",
                Dark = "#2a2a55",
                TextPrimary = "#f4f4ff",
                TextSecondary = "#a9add6",
                TextDisabled = "rgba(244,244,255,0.38)",
                ActionDefault = "#c9ccf0",
                ActionDisabled = "rgba(244,244,255,0.3)",
                ActionDisabledBackground = "rgba(244,244,255,0.12)",
                Background = "#0a0a1f",
                BackgroundGray = "#060612",
                Surface = "#15153a",
                DrawerBackground = "#0f0f2d",
                DrawerText = "#f4f4ff",
                DrawerIcon = "#29e6ff",
                AppbarBackground = "#000000",
                AppbarText = "#ffd23f",
                LinesDefault = "#3a3af0",
                LinesInputs = "#7d82c9",
                TableLines = "#2c2c6e",
                TableStriped = "rgba(255,255,255,0.03)",
                Divider = "#2c2c6e",
                DividerLight = "rgba(255,255,255,0.08)"
            },
            Typography = new Typography
            {
                Default = new DefaultTypography { FontFamily = body },
                H1 = new H1Typography { FontFamily = heading, FontSize = "2.5rem", FontWeight = "400", LineHeight = "1.5" },
                H2 = new H2Typography { FontFamily = heading, FontSize = "2rem", FontWeight = "400", LineHeight = "1.5" },
                H3 = new H3Typography { FontFamily = heading, FontSize = "1.6rem", FontWeight = "400", LineHeight = "1.5" },
                H4 = new H4Typography { FontFamily = heading, FontSize = "1.3rem", FontWeight = "400", LineHeight = "1.5" },
                H5 = new H5Typography { FontFamily = heading, FontSize = "1.05rem", FontWeight = "400", LineHeight = "1.5" },
                H6 = new H6Typography { FontFamily = heading, FontSize = "0.9rem", FontWeight = "400", LineHeight = "1.6" },
                Subtitle1 = new Subtitle1Typography { FontFamily = body },
                Subtitle2 = new Subtitle2Typography { FontFamily = body },
                Body1 = new Body1Typography { FontFamily = body },
                Body2 = new Body2Typography { FontFamily = body },
                Button = new ButtonTypography { FontFamily = body, FontWeight = "700", LetterSpacing = "0.06em" },
                Caption = new CaptionTypography { FontFamily = body },
                Overline = new OverlineTypography { FontFamily = body }
            },
            LayoutProperties = new LayoutProperties { DefaultBorderRadius = "0px" },

            // No soft shadows on an 8-bit screen; app.css draws hard outlines in their place.
            Shadows = new Shadow { Elevation = [.. Enumerable.Repeat("none", new Shadow().Elevation.Length)] }
        };
    }
}
