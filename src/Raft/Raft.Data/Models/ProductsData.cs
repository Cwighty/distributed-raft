using Raft.Data.Models;

public static class ProductsData
{
    public static List<Product> GetProducts()
    {
        return new List<Product>
            {
                new Product(1, "Corne Keyboard", @"Layout: Split ergonomic with 3x6 column staggered keys per side and 3 thumb keys.
                Features: OLED display support, per-key RGB backlighting, hot-swappable PCB options.
                Connectivity: Wired (USB-C) or wireless options depending on the build.
                Programmability: Fully programmable via QMK or ZMK firmware."),
                new Product(2, "Lily58 Keyboard", @"Layout: Split with 6x4 keys per side and 4 thumb keys.
                Features: Supports OLED displays and RGB underglow, hot-swappable PCB options.
                Connectivity: Wired (USB-C).
                Programmability: Fully programmable via QMK firmware."),
                new Product(3, "Sofle Keyboard", @"Layout: Split ergonomic with 6x4 grid and 5 thumb keys per side.
                Features: Supports for rotary encoders and OLED screens, RGB underglow.
                Connectivity: Wired (USB-C).
                Programmability: Fully programmable via QMK firmware."),
                new Product(4, "Iris Keyboard", @"Layout: Split ergonomic with 5x6 grid and 3 thumb keys, optional 1-2 extra keys per hand.
                Features: Optional OLED display, RGB underglow.
                Connectivity: Wired (USB-C).
                Programmability: Fully programmable via QMK firmware."),
                new Product(5, "Kyria Keyboard", @"Layout: Split ergonomic with 5x6 grid and 3 thumb keys per side.
                Features: OLED display support, options for rotary encoders, RGB underglow.
                Connectivity: Wired (USB-C) or Bluetooth options.
                Programmability: Fully programmable via QMK or ZMK firmware for builds."),
            };
    }
}
