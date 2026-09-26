// Интерьеры квартир из DLC GTA5RP_APARTMENT (client_packages/dlcpacks/GTA5RP_APARTMENT/dlc.rpf).
// Карты интерьеров в DLC могут быть не активны по умолчанию — подгружаем их как IPL.
// Сервер: dotnet/resources/NeptuneEvo/Houses/Apartments/ApartmentInteriors.cs
const APARTMENT_IPLS = [
    "int_ap_house_1_1_milo_", "int_ap_house_1_2_milo_", "int_ap_house_1_3_milo_", "int_ap_house_1_4_milo_", "int_ap_house_1_5_milo_",
    "int_garage_milo_",
    // квартиры «лофт» (clawles)
    "int_clawles_apartment_style_1_milo_", "int_clawles_apartment_style_2_milo_", "int_clawles_apartment_style_3_milo_",
    "int_clawles_apartment_style_4_milo_", "int_clawles_apartment_style_5_milo_",
    // коридоры, лестницы и номера квартир на дверях
    "kor_elit_1_milo_", "kor_med_1_milo_", "stair_elit_1_milo_", "stair_med_1_milo_",
    "kor_elit_1_num_stat", "kor_med_1_num_stat",
    "kor_bich1_1_milo_", "kor_bich2_1_milo_", "kor_bich3_1_milo_", "kor_bich4_1_milo_", "kor_bich5_1_milo_",
    "int_mansion_1_milo_",
];

const loadApartmentIpls = () => {
    APARTMENT_IPLS.forEach((ipl) => {
        try {
            if (!mp.game.streaming.isIplActive(ipl))
                mp.game.streaming.requestIpl(ipl);
        } catch (e) {}
    });
};

loadApartmentIpls();
mp.events.add('playerSpawn', loadApartmentIpls);
