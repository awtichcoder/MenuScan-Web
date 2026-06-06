export function initHeaderScroll() {
  const header = document.getElementById('main-header')
  const bookingBtn = document.getElementById('booking-btn')
  const logo = document.getElementById('logo')

  window.addEventListener('scroll', () => {
    if (window.scrollY > 150) {
      logo.classList.remove('brightness-0', 'invert')
      header.classList.remove('bg-transparent', 'text-white')
      header.classList.add('bg-white', 'text-black', 'shadow-md')

      bookingBtn.classList.remove(
        'border-current',
        'hover:bg-white',
        'hover:text-black'
      )
      bookingBtn.classList.add(
        'bg-[#E60012]',
        'text-white',
        'border-[#E60012]',
        'hover:bg-red-700'
      )
    } else {
      logo.classList.add('brightness-0', 'invert')
      header.classList.add('bg-transparent', 'text-white')
      header.classList.remove('bg-white', 'text-black', 'shadow-md')

      bookingBtn.classList.add(
        'border-current',
        'hover:bg-white',
        'hover:text-black'
      )
      bookingBtn.classList.remove(
        'bg-[#E60012]',
        'text-white',
        'border-[#E60012]',
        'hover:bg-red-700'
      )
    }
  })
}
