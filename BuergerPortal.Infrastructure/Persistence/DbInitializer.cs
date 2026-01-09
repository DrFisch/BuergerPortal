using BuergerPortal.Domain.Poi.Entity;
using BuergerPortal.Domain.Poi.Enums;
using BuergerPortal.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuergerPortal.Infrastructure.Database.Persistence
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(PortalDbContext context)
        {
            if (context.Pois.Any()) return;

            var pois = new List<PoiEntity>
            {
                // --- VERWALTUNG ---
                new PoiEntity { Id = Guid.NewGuid(), Name = "Rathaus Hof", Category = PoiCategory.Verwaltung, Description = "Hauptsitz der Stadtverwaltung, Oberbürgermeister und Bürgeramt.", Tags = "rathaus stadtverwaltung pass meldeamt ausweis hof", Icon = "bi-building", Address = "Klosterstraße 1, 95028 Hof", Latitude = 50.3218, Longitude = 11.9175 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Standesamt Hof", Category = PoiCategory.Verwaltung, Description = "Zuständig für Hochzeiten (Trausaal im Rathaus) und Urkundenwesen.", Tags = "heirat hochzeit geburt urkunde name", Icon = "bi-heart-fill", Address = "Klosterstraße 1, 95028 Hof", Latitude = 50.3218, Longitude = 11.9175 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Finanzamt Hof", Category = PoiCategory.Verwaltung, Description = "Zuständig für Steuererklärungen und Elster-Support.", Tags = "steuer geld finanzen einkommen", Icon = "bi-cash-stack", Address = "Hans-Böckler-Straße 4, 95030 Hof", Latitude = 50.3205, Longitude = 11.9055 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Kfz-Zulassungsstelle (Landratsamt)", Category = PoiCategory.Verwaltung, Description = "An- und Abmeldung von Fahrzeugen im Hofer Land.", Tags = "auto kfz schild kennzeichen fahrzeug", Icon = "bi-car-front", Address = "Schaumbergstraße 14, 95032 Hof", Latitude = 50.3115, Longitude = 11.9075 },

                // --- KULTUR ---
                new PoiEntity { Id = Guid.NewGuid(), Name = "Museum Bayerisches Vogtland", Category = PoiCategory.Kultur, Description = "Stadtgeschichte und Kultur des Vogtlandes im historischen Hospital.", Tags = "museum ausstellung kunst lernen vogtland", Icon = "bi-image", Address = "Sigmundsgraben 9, 95028 Hof", Latitude = 50.3225, Longitude = 11.9155 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "St. Marienkirche", Category = PoiCategory.Kultur, Description = "Wahrzeichen der Stadt Hof mit markanten Doppeltürmen.", Tags = "kirche dom religion turm geschichte", Icon = "bi-church", Address = "Lorenzstraße 1, 95028 Hof", Latitude = 50.3212, Longitude = 11.9190 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Theater Hof", Category = PoiCategory.Kultur, Description = "Vierspartentheater mit Oper, Schauspiel, Ballett und Konzerten.", Tags = "theater musik bühne kultur oper", Icon = "bi-masks", Address = "Kulmbacher Str. 5, 95030 Hof", Latitude = 50.3245, Longitude = 11.9065 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Freiheitshalle Hof", Category = PoiCategory.Kultur, Description = "Größte Veranstaltungshalle der Region für Konzerte und Messen.", Tags = "konzert event messe kultur halle", Icon = "bi-bank", Address = "Kulmbacher Str. 4, 95030 Hof", Latitude = 50.3255, Longitude = 11.9055 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Wartturm", Category = PoiCategory.Kultur, Description = "Historischer Aussichtsturm mit Rundblick über Hof.", Tags = "denkmal turm aussicht geschichte", Icon = "bi-trophy", Address = "Wartturmweg, 95028 Hof", Latitude = 50.3160, Longitude = 11.9380 },

                // --- FREIZEIT & NATUR ---
                new PoiEntity { Id = Guid.NewGuid(), Name = "Bürgerpark Theresienstein", Category = PoiCategory.Freizeit, Description = "Einer der schönsten Parks Deutschlands mit botanischem Garten.", Tags = "park natur spazieren erholung grün", Icon = "bi-tree", Address = "Alte Plauener Str., 95028 Hof", Latitude = 50.3295, Longitude = 11.9245 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Untreusee", Category = PoiCategory.Freizeit, Description = "Naherholungsgebiet zum Schwimmen, Segeln und Wandern.", Tags = "see wasser schwimmen natur erholung", Icon = "bi-water", Address = "Untreusee, 95032 Hof", Latitude = 50.2855, Longitude = 11.9165 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Zoologischer Garten Hof", Category = PoiCategory.Freizeit, Description = "Familienfreundlicher Zoo am Theresienstein.", Tags = "tiere kinder zoo ausflug", Icon = "bi-bug", Address = "Alte Plauener Str. 40, 95028 Hof", Latitude = 50.3315, Longitude = 11.9285 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Fernwehpark", Category = PoiCategory.Freizeit, Description = "Multikulturelles Friedensprojekt mit Ortsschildern aus aller Welt.", Tags = "tourismus schilder friedensprojekt", Icon = "bi-geo-alt", Address = "Oberer Winkel, 95028 Hof", Latitude = 50.3195, Longitude = 11.9150 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Kletterpark Untreusee", Category = PoiCategory.Freizeit, Description = "Herausfordernde Parcours direkt am Seeufer.", Tags = "sport klettern outdoor kinder", Icon = "bi-bicycle", Address = "Stauseestraße, 95032 Hof", Latitude = 50.2835, Longitude = 11.9145 },

                // --- ENTSORGUNG ---
                new PoiEntity { Id = Guid.NewGuid(), Name = "Wertstoffhof Hof (AZV)", Category = PoiCategory.Entsorgung, Description = "Zentrale Abgabestelle für Sperrmüll und Elektrogeräte.", Tags = "müll recycling schrott entsorgung", Icon = "bi-trash", Address = "Kirchbehl 1, 95032 Hof", Latitude = 50.3065, Longitude = 11.8955 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Kompostplatz Hof", Category = PoiCategory.Entsorgung, Description = "Abgabe von Grünschnitt und Gartenabfällen.", Tags = "garten laub holz kompost", Icon = "bi-leaf", Address = "An der Saale, 95028 Hof", Latitude = 50.3150, Longitude = 11.9250 },

                // --- BILDUNG ---
                new PoiEntity { Id = Guid.NewGuid(), Name = "Stadtbücherei Hof", Category = PoiCategory.Bildung, Description = "Große Auswahl an Büchern und digitalen Medien.", Tags = "buch lernen lesen schule medien", Icon = "bi-book", Address = "Wörthstraße 4, 95028 Hof", Latitude = 50.3185, Longitude = 11.9160 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "VHS Hofer Land", Category = PoiCategory.Bildung, Description = "Kurse für Sprachen, Gesundheit und Beruf.", Tags = "vhs lernen bildung kurs vhs", Icon = "bi-mortarboard", Address = "Ludwigstraße 7, 95028 Hof", Latitude = 50.3205, Longitude = 11.9185 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Hochschule Hof", Category = PoiCategory.Bildung, Description = "Campus der Hochschule für angewandte Wissenschaften.", Tags = "studium hochschule uni lernen", Icon = "bi-mortarboard-fill", Address = "Alfons-Goppel-Platz 1, 95028 Hof", Latitude = 50.3250, Longitude = 11.9395 },

                // --- MOBILITÄT ---
                new PoiEntity { Id = Guid.NewGuid(), Name = "Hauptbahnhof Hof", Category = PoiCategory.Mobilitaet, Description = "Zentraler Eisenbahnknotenpunkt Oberfrankens.", Tags = "bahn bahnhof reisen zug", Icon = "bi-train-front", Address = "Bahnhofstraße, 95028 Hof", Latitude = 50.3105, Longitude = 11.9215 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Bushof (ZOB)", Category = PoiCategory.Mobilitaet, Description = "Zentraler Umsteigepunkt für den Hofer Stadtbusverkehr.", Tags = "bus busbahnhof öpnv verkehr", Icon = "bi-bus-front", Address = "Bergstraße, 95028 Hof", Latitude = 50.3195, Longitude = 11.9165 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "E-Ladesäule Altstadt", Category = PoiCategory.Mobilitaet, Description = "Schnellladestation der Stadtwerke Hof.", Tags = "strom elektro laden auto", Icon = "bi-lightning-charge", Address = "Altstadt, 95028 Hof", Latitude = 50.3188, Longitude = 11.9178 },

                // --- NOTFALL ---
                new PoiEntity { Id = Guid.NewGuid(), Name = "Sana Klinikum Hof", Category = PoiCategory.Notfall, Description = "Krankenhaus der Versorgungsstufe II mit Notaufnahme.", Tags = "krankenhaus arzt hilfe unfall notfall", Icon = "bi-hospital", Address = "Eppenreuther Str. 9, 95032 Hof", Latitude = 50.3055, Longitude = 11.9015 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Hauptfeuerwache Hof", Category = PoiCategory.Notfall, Description = "Freiwillige Feuerwehr der Stadt Hof.", Tags = "feuerwehr brand hilfe notruf", Icon = "bi-shield-shaded", Address = "Hallplatz 1, 95028 Hof", Latitude = 50.3180, Longitude = 11.9135 },

                // --- WEITERE ERGÄNZUNGEN ---
                new PoiEntity { Id = Guid.NewGuid(), Name = "HofBad", Category = PoiCategory.Freizeit, Description = "Städtisches Hallenbad mit Saunalandschaft.", Tags = "schwimmen baden sauna sport", Icon = "bi-droplet", Address = "Oberer Anger 4, 95028 Hof", Latitude = 50.3155, Longitude = 11.9175 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Freibad Hof", Category = PoiCategory.Freizeit, Description = "Sommerbadespaß am Ascher Straße.", Tags = "schwimmen sommer freibad baden", Icon = "bi-brightness-high", Address = "Ascher Str. 250, 95028 Hof", Latitude = 50.3225, Longitude = 11.9485 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Kulturzentrum Alte Filzfabrik", Category = PoiCategory.Kultur, Description = "Alternative Kultur und Veranstaltungen in Hof.", Tags = "kultur event konzert szene", Icon = "bi-music-note-beamed", Address = "Quetschenweg 32, 95030 Hof", Latitude = 50.3115, Longitude = 11.9025 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Wochenmarkt Maxplatz", Category = PoiCategory.Kultur, Description = "Frische Lebensmittel direkt vom Erzeuger.", Tags = "markt einkaufen gemüse essen", Icon = "bi-shop", Address = "Maxplatz, 95028 Hof", Latitude = 50.3208, Longitude = 11.9175 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Jugendzentrum Q", Category = PoiCategory.Bildung, Description = "Treffpunkt für Jugendliche mit vielen Freizeitangeboten.", Tags = "jugend treff kinder hilfe", Icon = "bi-people", Address = "Hans-Böckler-Straße 4, 95030 Hof", Latitude = 50.3205, Longitude = 11.9055 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Musikschule der Hofer Symphoniker", Category = PoiCategory.Bildung, Description = "Exzellente Musikausbildung in der Saalestadt.", Tags = "musik lernen klavier geige symphoniker", Icon = "bi-music-player", Address = "Klosterstraße 9, 95028 Hof", Latitude = 50.3225, Longitude = 11.9180 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Stadtpark Saaleauen", Category = PoiCategory.Freizeit, Description = "Grünes Band entlang der Saale durch das Stadtgebiet.", Tags = "saale wandern spazieren natur", Icon = "bi-tree-fill", Address = "Saaleauen, 95028 Hof", Latitude = 50.3185, Longitude = 11.9225 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Altglascontainer Poststraße", Category = PoiCategory.Entsorgung, Description = "Sammelstelle für Glas-Recycling.", Tags = "glas recycling müll umwelt", Icon = "bi-box-seam", Address = "Poststraße, 95028 Hof", Latitude = 50.3200, Longitude = 11.9145 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Tierärztliche Klinik Hof", Category = PoiCategory.Notfall, Description = "Tierärztliche Versorgung für Kleintiere im Notfall.", Tags = "tier hund katze arzt notaufnahme", Icon = "bi-heart-pulse-fill", Address = "Luitpoldstraße 14, 95028 Hof", Latitude = 50.3165, Longitude = 11.9195 },
                new PoiEntity { Id = Guid.NewGuid(), Name = "Botanischer Garten (Theresienstein)", Category = PoiCategory.Freizeit, Description = "Historischer botanischer Garten mit Alpinum.", Tags = "blumen pflanzen natur garten", Icon = "bi-flower1", Address = "Alte Plauener Str. 16, 95028 Hof", Latitude = 50.3305, Longitude = 11.9265 }
            };

            await context.Pois.AddRangeAsync(pois);
            await context.SaveChangesAsync();
        }
    }
}