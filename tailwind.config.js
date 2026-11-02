/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.cshtml',
    './wwwroot/js/**/*.js'
  ],
  theme: {
    extend: {
      colors: {
        // Manoly brand palette (from the identity sheet). Token names kept
        // from the original design so every view picks up the new colours.
        paper:    '#FFFFFF',   // cards, bars
        canvas:   '#F4F4F5',   // page background
        linen:    '#F1F1F2',   // inputs, hover
        sand:     '#E3E4E5',   // hairlines
        bark: {
          DEFAULT: '#58595B',  // brand grey
          d:       '#3D3E40',
          l:       '#A7A9AC',  // brand light grey
        },
        walnut:   '#2B2B2C',
        ink:      '#111111',   // logo black
        brand: {
          DEFAULT: '#CA292E',  // Manoly red
          d:       '#871518',  // Manoly dark red
          tint:    '#FBECEC',
        },
        ember:    '#CA292E',   // accent → brand red
        rust:     '#871518',   // danger → brand dark red
        moss:     '#2F7A4A',   // success / received
        amber:    '#C98A1E',   // arriving
      },

      fontFamily: {
        // Brand type is Helvetica (Latin). The brand's Arabic face (Muna) is not
        // available on the web, so Arabic text uses IBM Plex Sans Arabic. It is
        // listed before Arial so Windows doesn't fall back to Arial's Arabic.
        display: ['"Helvetica Neue"', 'Helvetica', '"IBM Plex Sans Arabic"', 'Arial', 'sans-serif'],
        arabic:  ['"IBM Plex Sans Arabic"', 'system-ui', 'sans-serif'],
        ui:      ['"Helvetica Neue"', 'Helvetica', '"IBM Plex Sans Arabic"', 'Arial', 'sans-serif'],
        sans:    ['"IBM Plex Sans Arabic"', '"Helvetica Neue"', 'Helvetica', 'Arial', 'system-ui', 'sans-serif'],
      },

      fontSize: {
        '2xs':  ['10px',  { lineHeight: '14px', letterSpacing: '0' }],
        'xs':   ['11px',  { lineHeight: '16px' }],
        'sm':   ['14px',  { lineHeight: '20px' }],
        'base': ['15px',  { lineHeight: '22px' }],
        'lg':   ['18px',  { lineHeight: '26px' }],
        'xl':   ['22px',  { lineHeight: '28px' }],
        '2xl':  ['28px',  { lineHeight: '34px' }],
        '3xl':  ['34px',  { lineHeight: '40px' }],
        '4xl':  ['40px',  { lineHeight: '46px' }],
      },

      borderRadius: {
        none:    '0',
        sm:      '4px',
        DEFAULT: '8px',
        md:      '10px',
        lg:      '12px',
        xl:      '16px',
        full:    '9999px',
      },

      spacing: {
        '18':  '4.5rem',
      },

      boxShadow: {
        none: 'none',
        card: '0 1px 2px rgba(17,17,17,0.04), 0 1px 1px rgba(17,17,17,0.03)',
      },

      animation: {
        'fade-in':  'fadeIn 200ms ease-out',
        'slide-up': 'slideUp 280ms cubic-bezier(0.16, 1, 0.3, 1)',
      },

      keyframes: {
        fadeIn: {
          '0%':   { opacity: '0' },
          '100%': { opacity: '1' },
        },
        slideUp: {
          '0%':   { opacity: '0', transform: 'translateY(12px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
      },
    },
  },
  plugins: [],
}
