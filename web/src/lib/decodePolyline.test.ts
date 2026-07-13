import { describe, expect, it } from 'vitest'
import { decodePolyline } from './decodePolyline'

describe('decodePolyline', () => {
  it('decodifica una polyline conocida de Google', () => {
    // Encodes roughly (_p~iF~ps|U_ulLnnqC_mqNvxq`@)
    const points = decodePolyline('_p~iF~ps|U_ulLnnqC_mqNvxq`@')
    expect(points).toHaveLength(3)
    expect(points[0].lat).toBeCloseTo(38.5, 4)
    expect(points[0].lng).toBeCloseTo(-120.2, 4)
    expect(points[1].lat).toBeCloseTo(40.7, 4)
    expect(points[1].lng).toBeCloseTo(-120.95, 4)
    expect(points[2].lat).toBeCloseTo(43.252, 4)
    expect(points[2].lng).toBeCloseTo(-126.453, 4)
  })

  it('devuelve vacío para cadena vacía', () => {
    expect(decodePolyline('')).toEqual([])
  })
})
