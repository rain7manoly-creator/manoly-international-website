/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.cshtml',
    './wwwroot/js/**/*.js'
  ],
  theme: {
    extend: {
      colors: {
        // Warm Editorial palette
        paper:    '#FAF8F4',
        linen:    '#F1ECE3',
        sand:     '#E4DCCC',
        bark: {
          DEFAULT: '#847569',
          d:       '#5C5246',
          l:       '#A8A095',
        },
        walnut:   '#443830',
        ink:      '#1F1B17',
        ember:    '#B86B3A',
        moss:     '#4A6B3F',
        amber:    '#C8893B',
        rust:     '#A8443A',
      },

      fontFamily: {
        display: ['"Fraunces"', 'Georgia', 'serif'],
        arabic:  ['"IBM Plex Sans Arabic"', 'system-ui', 'sans-serif'],
        ui:      ['"Inter"', 'system-ui', 'sans-serif'],
        sans:    ['"IBM Plex Sans Arabic"', '"Inter"', 'system-ui', 'sans-serif'],
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
        'nav': '64px',
        '18':  '4.5rem',
      },

      // No shadows — design spec says no shadows anywhere
      boxShadow: {
        none: 'none',
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
